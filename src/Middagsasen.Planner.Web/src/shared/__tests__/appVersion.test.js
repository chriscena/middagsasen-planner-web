import { vi, describe, it, expect, beforeEach } from 'vitest';
import {
  createVersionChecker,
  tryReload,
  formatVersion,
  isChunkLoadError,
} from 'src/shared/appVersion';

function jsonResponse(body, ok = true) {
  return { ok, json: () => Promise.resolve(body) };
}

describe('createVersionChecker', () => {
  let time;
  const now = () => time;

  beforeEach(() => {
    time = 1_000_000;
  });

  it('returns true when the remote version differs', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse({ version: 'v2' }));
    const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });

    await expect(checker.check()).resolves.toBe(true);
    expect(fetchFn).toHaveBeenCalledWith('/version.json?t=1000000', {
      cache: 'no-store',
    });
    expect(checker.updateAvailable).toBe(true);
  });

  it('returns false when the remote version is the same', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse({ version: 'v1' }));
    const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });

    await expect(checker.check()).resolves.toBe(false);
  });

  it('throttles checks within throttleMs', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse({ version: 'v1' }));
    const checker = createVersionChecker({
      currentVersion: 'v1',
      fetchFn,
      now,
      throttleMs: 1000,
    });

    await checker.check();
    time += 999;
    await checker.check();
    expect(fetchFn).toHaveBeenCalledTimes(1);

    time += 1;
    await checker.check();
    expect(fetchFn).toHaveBeenCalledTimes(2);
  });

  it('force bypasses the throttle', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse({ version: 'v1' }));
    const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });

    await checker.check();
    await checker.check({ force: true });
    expect(fetchFn).toHaveBeenCalledTimes(2);
  });

  it('returns false silently on network errors, non-ok responses and invalid JSON', async () => {
    const failing = [
      vi.fn().mockRejectedValue(new TypeError('Failed to fetch')),
      vi.fn().mockResolvedValue(jsonResponse({ version: 'v2' }, false)),
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.reject(new SyntaxError('Unexpected token')),
      }),
      vi.fn().mockResolvedValue(jsonResponse({ noVersion: true })),
    ];

    for (const fetchFn of failing) {
      const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });
      await expect(checker.check()).resolves.toBe(false);
    }
  });

  it('remembers a detected update and stops fetching', async () => {
    const fetchFn = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ version: 'v2' }))
      .mockResolvedValue(jsonResponse({ version: 'v1' }));
    const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });

    await expect(checker.check()).resolves.toBe(true);
    await expect(checker.check({ force: true })).resolves.toBe(true);
    time += 10 * 60 * 1000;
    await expect(checker.check()).resolves.toBe(true);
    expect(fetchFn).toHaveBeenCalledTimes(1);
  });
});

describe('tryReload', () => {
  function createStorage() {
    const data = {};
    return {
      getItem: vi.fn((k) => (k in data ? data[k] : null)),
      setItem: vi.fn((k, v) => {
        data[k] = v;
      }),
    };
  }

  let location;
  beforeEach(() => {
    location = { assign: vi.fn(), reload: vi.fn() };
  });

  it('navigates to path the first time', () => {
    const storage = createStorage();
    const result = tryReload({ location, path: '/hours', storage, now: () => 1000 });

    expect(result).toBe(true);
    expect(location.assign).toHaveBeenCalledWith('/hours');
  });

  it('reloads current page when no path is given', () => {
    tryReload({ location, storage: createStorage(), now: () => 1000 });
    expect(location.reload).toHaveBeenCalledTimes(1);
  });

  it('blocks a second reload for the same reason within the guard window', () => {
    const storage = createStorage();
    let time = 1000;
    const now = () => time;

    tryReload({ location, path: '/a', storage, now, reason: 'chunk' });
    time += 9_999;
    expect(tryReload({ location, path: '/a', storage, now, reason: 'chunk' })).toBe(false);
    expect(location.assign).toHaveBeenCalledTimes(1);

    // Annen årsak sperres ikke.
    expect(tryReload({ location, path: '/a', storage, now, reason: 'other' })).toBe(true);

    time += 1;
    expect(tryReload({ location, path: '/a', storage, now, reason: 'chunk' })).toBe(true);
    expect(location.assign).toHaveBeenCalledTimes(3);
  });

  it('still reloads when storage throws', () => {
    const storage = {
      getItem: () => {
        throw new Error('denied');
      },
      setItem: () => {
        throw new Error('denied');
      },
    };
    expect(tryReload({ location, path: '/', storage, now: () => 1 })).toBe(true);
    expect(location.assign).toHaveBeenCalledWith('/');
  });
});

describe('formatVersion', () => {
  it('formats builtAt in local time with sha', () => {
    const builtAt = new Date(2026, 9, 1, 14, 32).toISOString();
    expect(formatVersion({ builtAt, sha: '9dd31cd' })).toBe('v 2026-10-01 14:32 · 9dd31cd');
  });

  it('omits sha when missing', () => {
    const builtAt = new Date(2026, 0, 5, 7, 3).toISOString();
    expect(formatVersion({ builtAt, sha: '' })).toBe('v 2026-01-05 07:03');
  });
});

describe('isChunkLoadError', () => {
  it('matches known browser messages', () => {
    expect(isChunkLoadError(new TypeError('Failed to fetch dynamically imported module: /assets/x.js'))).toBe(true);
    expect(isChunkLoadError(new TypeError('Importing a module script failed.'))).toBe(true);
    expect(isChunkLoadError(new Error('error loading dynamically imported module'))).toBe(true);
    expect(isChunkLoadError(new Error('Something else'))).toBe(false);
    expect(isChunkLoadError(undefined)).toBe(false);
  });
});
