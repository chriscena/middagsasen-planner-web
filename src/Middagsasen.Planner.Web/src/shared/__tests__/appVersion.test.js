import { vi, describe, it, expect, beforeEach } from 'vitest';
import {
  createVersionChecker,
  reloadOnce,
  decideNavigation,
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
    expect(checker.latestVersion).toBe('v2');
  });

  it('returns false when the remote version is the same', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse({ version: 'v1' }));
    const checker = createVersionChecker({ currentVersion: 'v1', fetchFn, now });

    await expect(checker.check()).resolves.toBe(false);
    expect(checker.updateAvailable).toBe(false);
    expect(checker.latestVersion).toBeNull();
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
    time += 1;
    await expect(checker.check()).resolves.toBe(true);
    time += 10 * 60 * 1000;
    await expect(checker.check()).resolves.toBe(true);
    expect(fetchFn).toHaveBeenCalledTimes(1);
  });
});

function createStorage() {
  const data = {};
  return {
    getItem: vi.fn((k) => (k in data ? data[k] : null)),
    setItem: vi.fn((k, v) => {
      data[k] = v;
    }),
  };
}

describe('reloadOnce', () => {
  let location;
  beforeEach(() => {
    location = { assign: vi.fn(), reload: vi.fn() };
  });

  it('navigates to path with assign', () => {
    const result = reloadOnce({ location, storage: createStorage(), key: 'remote:v2', path: '/hours' });

    expect(result).toBe(true);
    expect(location.assign).toHaveBeenCalledWith('/hours');
    expect(location.reload).not.toHaveBeenCalled();
  });

  it('reloads current page when no path is given', () => {
    reloadOnce({ location, storage: createStorage(), key: 'remote:v2' });
    expect(location.reload).toHaveBeenCalledTimes(1);
    expect(location.assign).not.toHaveBeenCalled();
  });

  it('reloads only once per key, but again for a new key', () => {
    const storage = createStorage();

    expect(reloadOnce({ location, storage, key: 'remote:v2', path: '/a' })).toBe(true);
    expect(reloadOnce({ location, storage, key: 'remote:v2', path: '/a' })).toBe(false);
    expect(location.assign).toHaveBeenCalledTimes(1);

    expect(reloadOnce({ location, storage, key: 'remote:v3', path: '/a' })).toBe(true);
    expect(location.assign).toHaveBeenCalledTimes(2);
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
    expect(reloadOnce({ location, storage, key: 'chunk:v1', path: '/' })).toBe(true);
    expect(location.assign).toHaveBeenCalledWith('/');
  });

  it('still reloads when storage is missing', () => {
    expect(reloadOnce({ location, storage: null, key: 'chunk:v1' })).toBe(true);
    expect(location.reload).toHaveBeenCalledTimes(1);
  });
});

describe('decideNavigation', () => {
  function createChecker({ updateAvailable = false, latestVersion = null } = {}) {
    return {
      updateAvailable,
      latestVersion,
      // Løses aldri: viser at guarden ikke venter på nettverket.
      check: vi.fn(() => new Promise(() => {})),
    };
  }

  const from = { path: '/events', fullPath: '/events' };
  const to = { path: '/hours', fullPath: '/hours?page=2' };

  it('skips everything on the start location', () => {
    const checker = createChecker({ updateAvailable: true, latestVersion: 'v2' });
    const reload = vi.fn();

    expect(decideNavigation({ to, from, isStartLocation: true, checker, reload })).toBe(true);
    expect(reload).not.toHaveBeenCalled();
    expect(checker.check).not.toHaveBeenCalled();
  });

  it('reloads to the target when an update is known', () => {
    const checker = createChecker({ updateAvailable: true, latestVersion: 'v2' });
    const reload = vi.fn(() => true);

    expect(decideNavigation({ to, from, isStartLocation: false, checker, reload })).toBe(false);
    expect(reload).toHaveBeenCalledWith({ key: 'remote:v2', path: '/hours?page=2' });
  });

  it('continues navigation when reload is blocked', () => {
    const checker = createChecker({ updateAvailable: true, latestVersion: 'v2' });
    const reload = vi.fn(() => false);

    expect(decideNavigation({ to, from, isStartLocation: false, checker, reload })).toBe(true);
  });

  it('starts a background check without waiting when no update is known', () => {
    const checker = createChecker();
    const reload = vi.fn();

    const result = decideNavigation({ to, from, isStartLocation: false, checker, reload });
    expect(result).toBe(true);
    expect(checker.check).toHaveBeenCalledTimes(1);
    expect(reload).not.toHaveBeenCalled();
  });

  it('does not reload when only query or hash changes', () => {
    const checker = createChecker({ updateAvailable: true, latestVersion: 'v2' });
    const reload = vi.fn(() => true);
    const sameTo = { path: '/events', fullPath: '/events?week=3#top' };

    expect(decideNavigation({ to: sameTo, from, isStartLocation: false, checker, reload })).toBe(true);
    expect(reload).not.toHaveBeenCalled();
    expect(checker.check).toHaveBeenCalledTimes(1);
  });

  it('swallows a rejected background check', async () => {
    const checker = createChecker();
    checker.check = vi.fn(() => Promise.reject(new Error('boom')));

    expect(decideNavigation({ to, from, isStartLocation: false, checker, reload: vi.fn() })).toBe(true);
    await Promise.resolve();
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
    expect(isChunkLoadError('Failed to fetch dynamically imported module')).toBe(false);
  });
});
