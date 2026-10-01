import { vi, describe, it, expect, beforeEach } from 'vitest';
import { handleUnauthorized, isSafeRedirect } from 'src/auth/unauthorizedHandler';

function createError(status, url) {
  const error = new Error('Request failed');
  error.config = { url };
  if (status !== undefined) error.response = { status };
  return error;
}

describe('handleUnauthorized', () => {
  let authStore;
  let router;
  let notify;

  function deps() {
    return { authStore, router, notify };
  }

  beforeEach(() => {
    authStore = {
      user: { id: 1 },
      removeUserSession: vi.fn(() => {
        authStore.user = null;
      }),
    };
    router = {
      currentRoute: { value: { path: '/hours', fullPath: '/hours' } },
      replace: vi.fn(),
    };
    notify = vi.fn();
  });

  it('clears session, notifies once and redirects to login on 401 from API', async () => {
    const error = createError(401, '/api/events');

    await expect(handleUnauthorized(error, deps())).rejects.toBe(error);

    expect(authStore.removeUserSession).toHaveBeenCalledTimes(1);
    expect(notify).toHaveBeenCalledTimes(1);
    expect(notify).toHaveBeenCalledWith({
      message: 'Du er logget ut. Logg inn på nytt.',
    });
    expect(router.replace).toHaveBeenCalledWith({
      path: '/login',
      query: { redirect: '/hours' },
    });
  });

  it('only notifies and redirects for the first of several parallel 401s', async () => {
    const first = createError(401, '/api/events');
    const second = createError(401, '/api/me');

    await expect(handleUnauthorized(first, deps())).rejects.toBe(first);
    await expect(handleUnauthorized(second, deps())).rejects.toBe(second);

    expect(authStore.removeUserSession).toHaveBeenCalledTimes(2);
    expect(notify).toHaveBeenCalledTimes(1);
    expect(router.replace).toHaveBeenCalledTimes(1);
  });

  it('ignores 401 from authentication endpoints', async () => {
    const error = createError(401, '/api/authentication/authenticate');

    await expect(handleUnauthorized(error, deps())).rejects.toBe(error);

    expect(authStore.removeUserSession).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it('clears stale session but does not notify or navigate when no user is loaded', async () => {
    authStore.user = null;
    const error = createError(401, '/api/events');

    await expect(handleUnauthorized(error, deps())).rejects.toBe(error);

    expect(authStore.removeUserSession).toHaveBeenCalledTimes(1);
    expect(notify).not.toHaveBeenCalled();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it('does not notify or navigate when already on login page', async () => {
    router.currentRoute.value = { path: '/login', fullPath: '/login' };
    const error = createError(401, '/api/me');

    await expect(handleUnauthorized(error, deps())).rejects.toBe(error);

    expect(authStore.removeUserSession).toHaveBeenCalledTimes(1);
    expect(notify).not.toHaveBeenCalled();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it.each([
    ['403', createError(403, '/api/events')],
    ['500', createError(500, '/api/events')],
    ['network error', createError(undefined, '/api/events')],
  ])('passes %s through untouched', async (_, error) => {
    await expect(handleUnauthorized(error, deps())).rejects.toBe(error);

    expect(authStore.removeUserSession).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
    expect(router.replace).not.toHaveBeenCalled();
  });
});

describe('isSafeRedirect', () => {
  it.each([
    ['/hours', true],
    ['/edit/12?x=1', true],
    ['//evil.com', false],
    ['/\\evil.com', false],
    ['https://evil.com', false],
    ['', false],
    [undefined, false],
    [['/hours'], false],
  ])('isSafeRedirect(%j) is %s', (path, expected) => {
    expect(isSafeRedirect(path)).toBe(expected);
  });
});
