// HTTP and CSRF only. This module does not read or change the page.
let csrfToken = '';

async function request(path, method = 'GET', body) {
  const headers = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = csrfToken;

  const response = await fetch('/api' + path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    const messages = {
      400: 'Sprawdź dane formularza lub odśwież stronę.',
      401: 'Sesja wygasła. Zaloguj się ponownie.',
      404: 'Notatka już nie istnieje. Odśwież listę.'
    };
    const error = new Error(data.message || messages[response.status] || 'Nie udało się wykonać operacji. Spróbuj ponownie.');
    error.status = response.status;
    throw error;
  }

  return response.status === 204 ? null : response.json();
}

export async function refreshCsrf() {
  csrfToken = (await request('/auth/csrf')).token;
}

export const auth = {
  me: () => request('/auth/me'),
  async login(email, password) {
    await refreshCsrf();
    return request('/auth/login', 'POST', { email, password });
  },
  async logout() {
    await request('/auth/logout', 'POST');
    csrfToken = '';
  }
};

export const notesApi = {
  list: query => request('/notes?q=' + encodeURIComponent(query)),
  save: (id, note) => request(id ? '/notes/' + id : '/notes', id ? 'PUT' : 'POST', note),
  delete: id => request('/notes/' + id, 'DELETE')
};
