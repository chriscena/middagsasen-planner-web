import { describe, it, expect } from 'vitest';
import {
  getWorkHourChanges,
  buildWorkHourPatch,
  getWorkHourErrorKind,
  getWorkHourErrorMessage,
  summarizeBulkApproval,
} from 'src/shared/workHourDiff';

const original = {
  startDateTime: '2026-01-10T08:00:00.000Z',
  endDateTime: '2026-01-10T12:00:00.000Z',
  description: 'Preparering',
};

describe('getWorkHourChanges', () => {
  it('returns empty diff for unchanged form', () => {
    expect(getWorkHourChanges(original, { ...original })).toEqual({});
  });

  it('compares times by instant, not string format', () => {
    const current = {
      ...original,
      startDateTime: '2026-01-10T08:00:00Z',
      endDateTime: '2026-01-10T13:00:00.000+01:00',
    };
    expect(getWorkHourChanges(original, current)).toEqual({});
  });

  it('returns only endTime when end time changed', () => {
    const current = { ...original, endDateTime: '2026-01-10T13:00:00.000Z' };
    expect(getWorkHourChanges(original, current)).toEqual({
      endTime: '2026-01-10T13:00:00.000Z',
    });
  });

  it('returns only startTime when start time changed', () => {
    const current = { ...original, startDateTime: '2026-01-10T07:30:00.000Z' };
    expect(getWorkHourChanges(original, current)).toEqual({
      startTime: '2026-01-10T07:30:00.000Z',
    });
  });

  it('returns only description when description changed', () => {
    const current = { ...original, description: 'Heiskjøring' };
    expect(getWorkHourChanges(original, current)).toEqual({
      description: 'Heiskjøring',
    });
  });

  it('treats null and empty description as equal', () => {
    expect(
      getWorkHourChanges(
        { ...original, description: null },
        { ...original, description: '' }
      )
    ).toEqual({});
  });
});

describe('buildWorkHourPatch', () => {
  it('approve without changes sends only approvalStatus', () => {
    expect(buildWorkHourPatch(original, { ...original }, 1)).toEqual({
      approvalStatus: 1,
    });
  });

  it('reject without changes sends only approvalStatus 2', () => {
    expect(buildWorkHourPatch(original, { ...original }, 2)).toEqual({
      approvalStatus: 2,
    });
  });

  it('changed description + approve sends description and approvalStatus', () => {
    const current = { ...original, description: 'Ny tekst' };
    expect(buildWorkHourPatch(original, current, 1)).toEqual({
      description: 'Ny tekst',
      approvalStatus: 1,
    });
  });

  it('without approval returns only changed fields', () => {
    const current = { ...original, endDateTime: '2026-01-10T14:00:00.000Z' };
    expect(buildWorkHourPatch(original, current)).toEqual({
      endTime: '2026-01-10T14:00:00.000Z',
    });
  });

  it('ignores approvalStatus other than 1 or 2', () => {
    expect(buildWorkHourPatch(original, { ...original }, null)).toEqual({});
    expect(buildWorkHourPatch(original, { ...original }, 0)).toEqual({});
  });

  it('never includes userId', () => {
    const withUser = { ...original, userId: 1 };
    const current = {
      startDateTime: '2026-01-10T09:00:00.000Z',
      endDateTime: '2026-01-10T15:00:00.000Z',
      description: 'Endret',
      userId: 2,
    };
    const patch = buildWorkHourPatch(withUser, current, 1);
    expect(patch).not.toHaveProperty('userId');
    expect(Object.keys(patch).sort()).toEqual(
      ['approvalStatus', 'description', 'endTime', 'startTime'].sort()
    );
  });
});

describe('getWorkHourErrorKind', () => {
  it('maps 409 to conflict', () => {
    expect(getWorkHourErrorKind({ response: { status: 409 } })).toBe('conflict');
  });
  it('maps 403 to forbidden', () => {
    expect(getWorkHourErrorKind({ response: { status: 403 } })).toBe('forbidden');
  });
  it('maps 404 to notFound', () => {
    expect(getWorkHourErrorKind({ response: { status: 404 } })).toBe('notFound');
  });
  it('maps other errors to other', () => {
    expect(getWorkHourErrorKind({ response: { status: 500 } })).toBe('other');
    expect(getWorkHourErrorKind(new Error('network'))).toBe('other');
    expect(getWorkHourErrorKind(undefined)).toBe('other');
  });
});

describe('getWorkHourErrorMessage', () => {
  it('returns server error message from body', () => {
    const error = {
      response: {
        status: 409,
        data: { error: 'Timeføringen har ingen status som kan fjernes.' },
      },
    };
    expect(getWorkHourErrorMessage(error, 'fallback')).toBe(
      'Timeføringen har ingen status som kan fjernes.'
    );
  });

  it('returns fallback when response has no body (empty 403)', () => {
    expect(
      getWorkHourErrorMessage({ response: { status: 403, data: '' } }, 'fallback')
    ).toBe('fallback');
    expect(
      getWorkHourErrorMessage({ response: { status: 403 } }, 'fallback')
    ).toBe('fallback');
  });

  it('returns fallback for ProblemDetails without error field', () => {
    const error = {
      response: {
        status: 400,
        data: {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { StartTime: ['The StartTime field is required.'] },
        },
      },
    };
    expect(getWorkHourErrorMessage(error, 'fallback')).toBe('fallback');
  });

  it('returns fallback for blank or non-string error', () => {
    expect(
      getWorkHourErrorMessage({ response: { data: { error: '  ' } } }, 'fallback')
    ).toBe('fallback');
    expect(
      getWorkHourErrorMessage({ response: { data: { error: 42 } } }, 'fallback')
    ).toBe('fallback');
  });

  it('returns fallback for network error without response', () => {
    expect(getWorkHourErrorMessage(new Error('Network Error'), 'fallback')).toBe(
      'fallback'
    );
    expect(getWorkHourErrorMessage(undefined, 'fallback')).toBe('fallback');
  });
});

describe('summarizeBulkApproval', () => {
  it('is positive when all succeeded', () => {
    expect(
      summarizeBulkApproval({ ok: 8, alreadyProcessed: 0, failed: 0 }, 1)
    ).toEqual({ type: 'positive', message: '8 godkjent' });
  });

  it('reports already processed as warning', () => {
    expect(
      summarizeBulkApproval({ ok: 8, alreadyProcessed: 2, failed: 0 }, 1)
    ).toEqual({
      type: 'warning',
      message: '8 godkjent, 2 var allerede behandlet',
    });
  });

  it('reports failures and uses avslått for rejection', () => {
    expect(
      summarizeBulkApproval({ ok: 3, alreadyProcessed: 1, failed: 1 }, 2)
    ).toEqual({
      type: 'warning',
      message: '3 avslått, 1 var allerede behandlet, 1 feilet',
    });
  });

  it('reports entries that no longer exist separately', () => {
    expect(
      summarizeBulkApproval(
        { ok: 5, alreadyProcessed: 1, notFound: 2, failed: 0 },
        1
      )
    ).toEqual({
      type: 'warning',
      message: '5 godkjent, 1 var allerede behandlet, 2 fantes ikke lenger',
    });
  });
});
