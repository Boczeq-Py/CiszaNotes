// DOM operations only. Application state and HTTP live in separate modules.
export const byId = id => document.getElementById(id);

const dateFormatter = new Intl.DateTimeFormat('pl-PL', {
  day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
});
const formatDate = value => dateFormatter.format(new Date(value));

export function setStatus(message = '', isError = false) {
  byId('status').textContent = message;
  byId('status').className = isError ? 'error' : '';
}

export function setDirty(dirty) {
  byId('dirty-label').textContent = dirty ? 'Niezapisane zmiany' : '';
}

export function setBusy(busy) {
  for (const id of ['save-note', 'delete-note', 'new-note', 'logout', 'note-title', 'note-content']) {
    byId(id).disabled = busy;
  }
}

export function updateCharacterCount() {
  const count = byId('note-content').value.length.toLocaleString('pl-PL');
  byId('char-count').textContent = count + ' / 20 000 znaków';
}

export function clearEditor() {
  byId('note-form').reset();
  byId('note-form').hidden = true;
  byId('empty-editor').hidden = false;
  byId('note-preview').textContent = '';
  setDirty(false);
  updateCharacterCount();
}

export function showLogin() {
  byId('login-view').hidden = false;
  byId('notes-view').hidden = true;
  byId('account').hidden = true;
  byId('local-badge').hidden = false;
  byId('email').textContent = '';
  byId('login-password').value = '';
  byId('search').value = '';
  byId('count').textContent = '0';
  byId('note-list').replaceChildren();
  byId('lab-xss-status').textContent = 'XSS: niewykonany';
  clearEditor();
}

export function showNotes(email) {
  byId('login-view').hidden = true;
  byId('notes-view').hidden = false;
  byId('account').hidden = false;
  byId('local-badge').hidden = true;
  byId('email').textContent = email;
  byId('login-password').value = '';
}

export function renderNotes(notes, selectedId, onSelect) {
  const list = byId('note-list');
  list.replaceChildren();
  byId('count').textContent = notes.length;

  if (!notes.length) {
    const message = document.createElement('p');
    message.className = 'list-empty';
    message.textContent = byId('search').value
      ? 'Brak pasujących notatek.'
      : 'Jeszcze tu pusto. Zapisz swoją pierwszą myśl.';
    list.append(message);
    return;
  }

  for (const note of notes) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'note-card' + (note.id === selectedId ? ' active' : '');
    button.setAttribute('aria-pressed', String(note.id === selectedId));

    const title = document.createElement('strong');
    title.innerHTML = note.title; // LAB: intentional stored XSS; never use with untrusted text.
    const preview = document.createElement('p');
    preview.textContent = note.content.slice(0, 95) || 'Pusta notatka';
    const time = document.createElement('time');
    time.dateTime = note.updatedAt;
    time.textContent = formatDate(note.updatedAt);

    button.append(title, preview, time);
    button.addEventListener('click', () => onSelect(note));
    list.append(button);
  }
}

export function showEditor(note) {
  byId('empty-editor').hidden = true;
  byId('note-form').hidden = false;
  byId('note-title').value = note?.title || '';
  byId('note-content').value = note?.content || '';
  byId('note-date').textContent = note ? 'OSTATNI ZAPIS · ' + formatDate(note.updatedAt) : 'NOWA NOTATKA';
  byId('delete-note').hidden = !note;
  byId('note-preview').innerHTML = note?.content || ''; // LAB: intentional stored XSS.
  updateCharacterCount();
}

export function readNote() {
  return { title: byId('note-title').value.trim(), content: byId('note-content').value };
}
