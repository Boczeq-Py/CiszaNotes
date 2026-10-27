# Cisza LAB — wariant podatny i zabezpieczony

Pobranie projektu z GitHuba:

```powershell
git clone https://github.com/Boczeq-Py/CiszaNotes.git
cd CiszaNotes
```

Dwa osobne projekty ASP.NET Core 10 Web API z EF Core/Npgsql, PostgreSQL, logowaniem cookie Identity i frontendem w `wwwroot`. Oba mają logowanie, wylogowanie, listę, wyszukiwanie, dodawanie, edycję i usuwanie notatek oraz podgląd zapisanej treści. Wariant jest stale oznaczony paskiem w `index.html`, tytułem karty i nagłówkiem odpowiedzi `X-Lab-Variant`.

**Wariant Vulnerable jest celowo podatny i służy wyłącznie lokalnym ćwiczeniom na fikcyjnych danych.** Nie wprowadzaj do niego prywatnych notatek. Wszystkie serwery i porty baz są przypięte do `127.0.0.1`; kod Kestrela nie pozwala wystawić laboratorium przez zmianę `--urls`. Aplikacje sprawdzają także adres klienta i lokalną nazwę bazy. Nie należy dodawać tuneli, proxy ani przekierowań portów do tego laboratorium.

| | Podatny — `Vulnerable/` | Zabezpieczony — `Secured/` |
| --- | --- | --- |
| Adres aplikacji | http://127.0.0.1:5181 | https://localhost:7182 |
| Wejście HTTP | Bez TLS | http://localhost:5182 → 307 do HTTPS |
| Baza | `notes_lab_vulnerable`, port 55441 | `notes_lab_secured`, port 55442 |
| Kontener / wolumen | `vulnerable-db` / `vulnerable-data` | `secured-db` / `secured-data` |
| Wyszukiwanie | Sklejanie SQL z danymi użytkownika | `FromSql(FormattableString)` — wartości jako parametry |
| Operacje po Id | Wyszukanie po samym Id | Id **i** właściciel z Identity |
| Wyjście HTML | Celowe `innerHTML` w tytule/podglądzie | `textContent` i `.value`, bez interpretacji HTML |
| CSRF | Brak walidacji tokena | Walidacja każdego POST/PUT/DELETE |
| Cookie sesji | Bez Secure i HttpOnly; SameSite=Lax | Secure, HttpOnly, SameSite=Strict, prefiks `__Host-` |
| CSP | Brak | Dodatkowa ochrona, bez skryptów inline |

Osobne kontenery nie mają wspólnej bazy ani wolumenu. Sesje i tokeny CSRF mają różne nazwy cookies, a Data Protection różne nazwy aplikacji. Nowy projekt nie korzysta z bazy wcześniejszej aplikacji PrivateNotes.

## Wymagania i pierwsze uruchomienie

- .NET SDK 10.
- Działający Docker Desktop / silnik Docker z Compose.
- PowerShell 7 dla skryptów inicjalizacji i testów.
- Zaufany certyfikat deweloperski ASP.NET Core dla `localhost`.

Wszystkie polecenia niżej uruchamiaj z folderu zawierającego ten README i `NotesLab.slnx`. Pierwsze pobranie obrazu i pakietów wymaga internetu, późniejsza praca nie korzysta z usług zewnętrznych.

Najpierw sprawdź certyfikat:

```powershell
dotnet dev-certs https --check --trust
```

Jeśli nie ma zaufanego certyfikatu, przygotuj go poleceniem `dotnet dev-certs https --trust` i zaakceptuj standardowy dialog systemu. Potem ponów sprawdzenie. Nie wyłączaj walidacji certyfikatu i nie omijaj ostrzeżeń przeglądarki. Certyfikatu ani klucza prywatnego nie dołączono do projektu; każda maszyna używa własnego certyfikatu. Dla HTTPS korzystaj z nazwy `localhost` zgodnej z certyfikatem.

Przygotuj obie bazy, migracje i dane:

```powershell
pwsh -NoProfile -File ./Initialize-Lab.ps1
```

Skrypt uruchamia dwa kontenery, przywraca pakiety i lokalne narzędzie `dotnet-ef`, a następnie wykonuje migracje oraz seed w każdej bazie. Zwykła inicjalizacja nie usuwa danych i nie dubluje istniejących kont.

Terminal 1 — podatny:

```powershell
dotnet run --project ./Vulnerable --no-launch-profile
```

Terminal 2 — zabezpieczony:

```powershell
dotnet run --project ./Secured --no-launch-profile
```

Otwórz adresy z tabeli. W obu wariantach dostępne są identyczne konta:

| E-mail | Hasło |
| --- | --- |
| anna@example.test | Anna!Notes2026 |
| piotr@example.test | Piotr!Notes2026 |

HTTPS chroni przesyłanie danych, a nie błędną autoryzację czy zapytania SQL. Samo dodanie TLS do wariantu podatnego nie usunęłoby SQL Injection, IDOR ani XSS. Przekierowanie HTTP nie cofa wysłania pierwszego żądania po HTTP; logowanie należy zaczynać bezpośrednio od adresu HTTPS. Porównanie dotyczy transportu przeglądarka–aplikacja; lokalne połączenia aplikacja–PostgreSQL są takie same w obu wariantach.

## Odtwarzanie danych

Przed kolejnym porównaniem zatrzymaj oba serwery przez Ctrl+C. Następnie:

```powershell
pwsh -NoProfile -File ./Initialize-Lab.ps1 -Reset
```

**`-Reset` usuwa i odtwarza wyłącznie dwie nazwane bazy laboratoryjne**, łącznie ze zmianami wykonanymi podczas ćwiczeń. Kod sprawdza przed resetem nazwę bazy, host `127.0.0.1` i dedykowany port. Potem ponownie uruchom dwa serwery i odśwież karty przeglądarki; w razie starej sesji zaloguj się ponownie.

Seed zawiera dwóch użytkowników o stałych Id (`lab-anna`, `lab-piotr`) i po trzy notatki. Id, tytuły, treści i daty początkowe są identyczne między wariantami. Sól i hash hasła oraz znaczniki bezpieczeństwa Identity są prawidłowo losowane; odtwarzalność nie oznacza identycznych hashy.

Najważniejsze stałe Id:

| Id notatki | Właściciel | Cel |
| --- | --- | --- |
| `10000000-0000-0000-0000-000000000003` | Anna | Zapisany XSS |
| `20000000-0000-0000-0000-000000000003` | Piotr | Fikcyjny `PIOTR_PRIVATE_MARKER`, test IDOR/SQLi |

## Automatyczne porównanie API

Po resecie i uruchomieniu obu serwerów, w trzecim terminalu:

```powershell
pwsh -NoProfile -File ./Test-Lab.ps1
```

Oczekiwany wynik: **66 asercji PASS**. Sukces w części podatnej oznacza również potwierdzenie podatności, a nie ich brak. Skrypt działa na prawdziwym PostgreSQL. Nie wyłącza walidacji certyfikatu. Testuje obie implementacje zwykłego CRUD, identyczny zestaw danych, niezależność baz, autoryzację, SQLi, IDOR odczytu/zmiany/usuwania, walidację CSRF, cookies oraz transport. Usuwa własne tymczasowe notatki i przywraca zmienioną treść notatki Piotra, ale aktualizuje datę jej ostatniego zapisu. Przed następnym pełnym porównaniem użyj resetu, aby przywrócić także daty.

Wynik wykonania: `TEST-RESULTS.md`. Dowody: `evidence/api-tests.txt`, `evidence/xss-dom.json` i zrzuty ekranu. Skrypt API sprawdza przechowywanie payloadu XSS, a jego **wykonanie** sprawdzono oddzielnie w przeglądarce.

## Ćwiczenie 1 — SQL Injection w wyszukiwaniu

Zaloguj Annę w obu wariantach. Zwykła lista ma 3 notatki Anny. Wpisz w wyszukiwarce dokładnie:

```text
') OR TRUE --
```

Podatny wariant zwraca wszystkie 6 notatek, w tym „Tylko dla Piotra”. W zabezpieczonym wariancie otrzymasz 0 wyników: cała fraza jest parametrem wyszukiwania, nie fragmentem SQL. Sprawdź też zwykłe wyszukiwanie `żółć`. W zabezpieczonym wariancie znaki `%`, `_` i `\` są wyszukiwane dosłownie.

Miejsce zmiany: `Controllers/NotesController.cs`, metoda `List`. Podatny kod przekazuje do `FromSqlRaw` już sklejony ciąg. Bezpieczny kod przekazuje interpolowany `FormattableString` bez wcześniejszej konwersji na `string`; EF tworzy parametry dla OwnerId i wzorca. To samo API `FromSqlRaw` także może być bezpieczne, gdy wartości przekazuje się jako parametry — tutaj błąd jest celowy i polega na sklejaniu danych z SQL.

## Ćwiczenie 2 — zmiana Id / IDOR

Po zalogowaniu jako Anna otwórz w nowej karcie danego wariantu:

```text
/api/notes/20000000-0000-0000-0000-000000000003
```

Podatny: HTTP 200 i treść z `PIOTR_PRIVATE_MARKER`. Zabezpieczony: HTTP 404. Nieistniejące i cudze Id mają ten sam wynik. Test automatyczny sprawdza też PUT i DELETE, potwierdzając zmianę lub usunięcie po stronie właściciela. Stałe GUID służą odtwarzalności ćwiczenia; trudniejszy do zgadnięcia GUID nie zastępuje kontroli właściciela.

Miejsce zmiany: `Controllers/NotesController.cs`, metody `Get`, `Update`, `Delete`. Bezpieczny wariant stosuje filtr `OwnerId` pobrany z zalogowanego Identity. Normalna lista w obu wersjach celowo filtruje po właścicielu — błąd IDOR ujawnia się dopiero po zmianie Id zasobu.

## Ćwiczenie 3 — zapisany XSS

Zaloguj Annę i otwórz notatkę **„LAB — zapisany XSS”**. Zapisana w PostgreSQL treść to:

```html
<img src="/lab-xss-missing" onerror="document.getElementById('lab-xss-status').textContent='XSS WYKONANY'">
```

Payload jest niegroźnym wskaźnikiem laboratoryjnym: zmienia wyłącznie tekst w lokalnym pasku, nie wykrada danych. Możesz również zapisać go jako treść nowej notatki, odświeżyć stronę i ponownie ją otworzyć.

- Podatny: pasek pokazuje **XSS WYKONANY**, w podglądzie powstaje element `<img>`, a jego `onerror` wykonuje kod.
- Zabezpieczony: pasek pozostaje **XSS: niewykonany**, podgląd wyświetla cały kod jako tekst i nie tworzy elementu `<img>`.

Miejsce zmiany: `wwwroot/js/view.js`. Bezpieczne `.textContent` i `.value` zachowują kontekst tekstowy, nie wymagając własnego, podatnego na błędy kodera HTML. Dodatkowo działa CSP, lecz dowód z DOM pokazuje, że już samo kodowanie wyjścia zapobiegło utworzeniu obrazu — to nie tylko zablokowany handler przez CSP. Przechowywanie payloadu jako danych nie jest samo w sobie błędem; błąd powstaje przy jego interpretacji jako HTML. Nie użyto `alert`, aby test nie wymagał zamykania dialogów.

## CSRF i cookies

Frontend obu wariantów potrafi pobierać token przez `GET /api/auth/csrf` i wysyłać go w `X-CSRF-TOKEN`. W wersji zabezpieczonej token jest wymagany także przy logowaniu i wylogowaniu; po zmianie tożsamości frontend pobiera nowy token. W podatnej backend go nie weryfikuje. Test API wysyła ten sam zapis bez tokena i z tokenem błędnym: podatny 201, zabezpieczony 400. Jest to test egzekwowania ochrony CSRF; nie symulowano pełnego ataku z zewnętrznej witryny. SameSite=Lax i brak CORS nadal ograniczają część scenariuszy przeglądarkowych w wersji podatnej.

W narzędziach deweloperskich, zakładce Network, porównaj `Set-Cookie` odpowiedzi logowania. Podatny: `NotesLab.Vulnerable.Session`, brak Secure/HttpOnly. Zabezpieczony: `__Host-NotesLab.Secured; path=/; secure; samesite=strict; httponly`. Cookie CSRF w wersji zabezpieczonej także ma Secure, HttpOnly i SameSite=Strict. Zabezpieczenia cookies i CSRF nie zastępują kodowania wyjścia przeciw XSS.

## Organizacja kodu

Kontrolery, kontrakty HTTP, model, konfiguracja i obsługa bazy są w osobnych folderach. Frontend używa trzech zwykłych modułów JavaScript: `wwwroot/js/api.js`, `view.js` i `app.js`. Nie wymaga Node.js ani bundlera. Oba warianty mają tę samą strukturę, co ułatwia porównanie różnic bezpieczeństwa.

Dokładna mapa plików i uzasadnienie pozostawionych/usuniętych fragmentów: [CODE-REVIEW.md](CODE-REVIEW.md). Narzędzie EF ma jeden manifest w katalogu rozwiązania; `dotnet tool restore` działa także z podfolderów projektów.

## Budowanie, migracje i zatrzymanie

```powershell
dotnet build NotesLab.slnx -c Release
```

Każdy projekt zawiera własną kopię migracji Identity/Notes, snapshot i SQL. Aby sprawdzić zgodność modelu lub zastosować wyłącznie migrację, wejdź do wybranego folderu `Vulnerable` albo `Secured`:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes
dotnet ef database update
```

Ctrl+C zatrzymuje dany serwer. `docker compose stop` zatrzymuje bazy i zachowuje wolumeny. Projekt używa jawnych haseł wyłącznie laboratoryjnych. Nie wprowadzono publicznej rejestracji; Identity nadal haszuje hasła w obu wariantach. „Zabezpieczony” oznacza ochronę przed omawianymi klasami błędów w tym ćwiczeniu, nie pełny audyt produktu ani szyfrowanie treści w bazie. Przy równoczesnej edycji ostatni zapis wygrywa.

Dokumentacja: [EF Core — parametryzacja SQL](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries), [CookieSecurePolicy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.cookiesecurepolicy), [ASP.NET Core — CSRF](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
