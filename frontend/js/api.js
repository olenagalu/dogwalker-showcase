const PrincessApi = (() => {
  // Fixed same-origin API: no configurable remote endpoint or shared live sessions.
  const baseUrl = window.location.origin;
  const tokenKey = 'dogwalkerShowcaseToken';
  const userKey = 'dogwalkerShowcaseUser';
  let redirectingToSignIn = false;

  function handleUnauthorized(response, path, token) {
    // An expired/revoked token must not leave a dashboard looking signed in.
    // Login errors stay on the login form, where its own feedback handles them.
    if (response.status !== 401 || !token || path.startsWith('/api/auth/')) return;
    sessionStorage.removeItem(tokenKey);
    sessionStorage.removeItem(userKey);
    if (redirectingToSignIn) return;
    redirectingToSignIn = true;
    const page = window.location.pathname.split('/').pop() || 'index.html';
    const returnTo = `${page}${window.location.search}${window.location.hash}`;
    window.location.replace(`auth.html?sessionExpired=1&returnTo=${encodeURIComponent(returnTo)}`);
  }

  function localPath(path) {
    if (typeof path !== 'string' || !path.startsWith('/api/') || path.includes('\\'))
      throw new Error('Only local showcase API paths are allowed.');
    const url = new URL(path, baseUrl);
    if (url.origin !== baseUrl) throw new Error('Remote API connections are disabled.');
    return url.href;
  }

  async function request(path, options = {}) {
    const token = sessionStorage.getItem(tokenKey);
    const isFormData = options.body instanceof FormData;
    const headers = { ...(options.body && !isFormData ? { 'Content-Type': 'application/json' } : {}), ...options.headers };
    if (token) headers.Authorization = `Bearer ${token}`;
    const response = await fetch(localPath(path), { ...options, headers });
    handleUnauthorized(response, path, token);
    const body = response.status === 204 ? null : await response.json().catch(() => ({}));
    if (!response.ok) {
      const message = body?.message || (body?.errors && Object.values(body.errors).flat().join(' ')) || body?.title || 'The request could not be completed.';
      const error = new Error(message);
      error.status = response.status;
      throw error;
    }
    return body;
  }

  async function privateImageUrl(path) {
    const token = sessionStorage.getItem(tokenKey);
    const response = await fetch(localPath(path), {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    handleUnauthorized(response, path, token);
    if (!response.ok) throw new Error('The private photo could not be loaded.');
    return URL.createObjectURL(await response.blob());
  }

  async function uploadReadyImage(file) {
    const maximumBytes = 2 * 1024 * 1024;
    if (!file || file.size <= maximumBytes) return file;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type))
      throw new Error('Use a JPEG, PNG, or WebP photo.');
    try {
      const bitmap = await createImageBitmap(file);
      const scale = Math.min(1, 1600 / Math.max(bitmap.width, bitmap.height));
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(bitmap.width * scale));
      canvas.height = Math.max(1, Math.round(bitmap.height * scale));
      canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      bitmap.close?.();
      for (const quality of [.86, .74, .62]) {
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
        if (blob && blob.size <= maximumBytes)
          return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type:'image/jpeg' });
      }
    } catch { /* Show the clear size message below instead of browser internals. */ }
    throw new Error('This photo is too large to prepare. Please choose a smaller photo.');
  }

  function setSession(response) {
    sessionStorage.setItem(tokenKey, response.token);
    sessionStorage.setItem(userKey, JSON.stringify(response.user));
  }

  function user() {
    try { return JSON.parse(sessionStorage.getItem(userKey)); } catch { return null; }
  }

  function signOut() {
    sessionStorage.removeItem(tokenKey);
    sessionStorage.removeItem(userKey);
    window.location.href = 'index.html';
  }

  function requireUser(role) {
    const current = user();
    if (!current || (role && current.role !== role)) {
      const page = window.location.pathname.split('/').pop() || 'dashboard.html';
      const returnTo = encodeURIComponent(`${page}${window.location.search}${window.location.hash}`);
      window.location.href = `auth.html?returnTo=${returnTo}`;
      return null;
    }
    return current;
  }

  return { request, privateImageUrl, uploadReadyImage, setSession, user, signOut, requireUser };
})();
