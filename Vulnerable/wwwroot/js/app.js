import { auth, notesApi, refreshCsrf } from './api.js';
import * as view from './view.js';

const { byId } = view;
let notes = [];
let selectedId = null;
let dirty = false;
let busy = false;
let searchTimer;
let listVersion = 0;

function markDirty(value) {
  dirty = value;
  view.setDirty(value);
}

function canLeave() {
  return !dirty || confirm('Masz niezapisane zmiany. Odrzucić je?');
}

function resetSession() {
  // Ignore a list response that arrives after logout or session expiry.
  listVersion++;
  clearTimeout(searchTimer);
  notes = [];
  selectedId = null;
  markDirty(false);
  view.showLogin();
}

function handleError(error) {
  if (error.status === 401) resetSession();
  view.setStatus(error.message, true);
}

async function runAction(action) {
  if (busy) return;
  busy = true;
  view.setBusy(true);
  try {
    await action();
  } catch (error) {
    handleError(error);
  } finally {
    busy = false;
    view.setBusy(false);
  }
}

function render() {
  view.renderNotes(notes, selectedId, note => {
    if (!busy && canLeave()) selectNote(note);
  });
}

async function loadNotes() {
  const version = ++listVersion;
  const result = await notesApi.list(byId('search').value);
  // A slower earlier search must not overwrite a more recent search.
  if (version !== listVersion) return;
  notes = result;
  render();
}

function selectNote(note) {
  selectedId = note?.id || null;
  markDirty(false);
  view.showEditor(note);
  render();
}

async function openWorkspace(email) {
  view.showNotes(email);
  await refreshCsrf(); // A login changes the identity bound to the CSRF token.
  await loadNotes();
}

byId('login-form').addEventListener('submit', async event => {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  byId('login-error').textContent = '';
  try {
    const user = await auth.login(byId('login-email').value, byId('login-password').value);
    await openWorkspace(user.email);
    view.setStatus();
  } catch (error) {
    resetSession();
    byId('login-error').textContent = error.message;
  } finally {
    button.disabled = false;
  }
});

byId('logout').addEventListener('click', () => {
  if (!canLeave()) return;
  runAction(async () => {
    await auth.logout();
    resetSession();
    view.setStatus('Wylogowano.');
  });
});

byId('new-note').addEventListener('click', () => {
  if (busy || !canLeave()) return;
  selectNote(null);
  view.setStatus();
  byId('note-title').focus();
});

byId('note-form').addEventListener('input', () => {
  markDirty(true);
  view.updateCharacterCount();
});

byId('note-form').addEventListener('submit', event => {
  event.preventDefault();
  const input = view.readNote();
  if (!input.title) {
    view.setStatus('Wpisz tytuł notatki.', true);
    byId('note-title').focus();
    return;
  }
  runAction(async () => {
    const note = await notesApi.save(selectedId, input);
    selectNote(note);
    await loadNotes();
    view.setStatus('Notatka zapisana.');
  });
});

byId('delete-note').addEventListener('click', () => {
  if (!selectedId || !confirm('Usunąć tę notatkę? Tej operacji nie można cofnąć.')) return;
  runAction(async () => {
    await notesApi.delete(selectedId);
    selectedId = null;
    markDirty(false);
    view.clearEditor();
    await loadNotes();
    view.setStatus('Notatka usunięta.');
  });
});

byId('search').addEventListener('input', () => {
  clearTimeout(searchTimer);
  listVersion++;
  searchTimer = setTimeout(() => loadNotes().catch(handleError), 250);
});

window.addEventListener('beforeunload', event => {
  if (!dirty) return;
  event.preventDefault();
  event.returnValue = '';
});

async function start() {
  try {
    const user = await auth.me();
    await openWorkspace(user.email);
  } catch (error) {
    resetSession();
    if (error.status !== 401) view.setStatus(error.message, true);
  }
}

start();
