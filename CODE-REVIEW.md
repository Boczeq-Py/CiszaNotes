# Przegląd i uproszczenie kodu

Zmiany dotyczą obu wariantów `NotesLab`. Pierwotna aplikacja w sąsiednim folderze `PrivateNotes` pozostaje osobnym projektem. Publiczne adresy API, porty, schemat baz i celowe różnice bezpieczeństwa laboratoriów zachowano.

## Podział odpowiedzialności

Każdy wariant ma ten sam układ:

```text
Program.cs                         uruchomienie i kolejność middleware
Configuration/LabConfiguration.cs  Kestrel, baza, Identity, cookies i CSRF
Middleware/LabRequestMiddleware.cs  loopback i nagłówki odpowiedzi
Controllers/AuthController.cs      logowanie, sesja, wylogowanie, token CSRF
Controllers/NotesController.cs     CRUD i wyszukiwanie
Contracts/LoginInput.cs            walidacja logowania
Contracts/NoteInput.cs             walidacja notatki
Contracts/NoteResponse.cs          jawny format odpowiedzi bez OwnerId
Models/Note.cs                     encja
Data/NotesDb.cs                    mapowanie EF Core / Identity
Data/LabSeed.cs                    fikcyjne dane o stałych Id
Data/DatabaseInitializer.cs        migracja i jawny reset danych laboratoryjnych
Migrations/                       migracja, snapshot i SQL
wwwroot/index.html                struktura strony
wwwroot/style.css                 sformatowany CSS
wwwroot/js/api.js                 żądania HTTP i token CSRF
wwwroot/js/view.js                tworzenie i czyszczenie elementów DOM
wwwroot/js/app.js                 stan, zdarzenia i przepływ pracy
```

W katalogu rozwiązania jest jeden manifest `dotnet-tools.json` dla narzędzia EF. Funkcje pomocnicze testów HTTP przeniesiono do `Tests/HttpHelpers.ps1`; scenariusze pozostały w `Test-Lab.ps1`.

## Co usunięto lub uproszczono

| Fragment | Decyzja i powód |
| --- | --- |
| `AddDefaultTokenProviders()` | Usunięto. Aplikacja nie udostępnia resetu hasła, potwierdzania e-maila ani 2FA wymagających tych providerów. CSRF korzysta z osobnego mechanizmu. |
| Powielony `appsettings.Development.json` | Usunięto. Zawierał te same ustawienia logowania co konfiguracja podstawowa. |
| Profil `Properties/launchSettings.json` | Usunięto. Porty ustala Kestrel, a pozostałe wartości profilu powielały ustawienia domyślne. Komendy uruchomienia pozostają takie same. |
| Kopie manifestu narzędzia i `.gitignore` | Zastąpiono wspólnymi plikami w katalogu rozwiązania. |
| `Controllers.cs` z kilkoma klasami | Zastąpiono osobnymi kontrolerami i kontraktami. |
| Anonimowe odpowiedzi notatek | Zastąpiono jednym jawnym `NoteResponse`, bez powtarzania mapowania. |
| API zmieniające stan strony | Rozdzielono HTTP od widoku; moduł API tylko zwraca dane lub błąd z kodem statusu. |
| Wykrywanie wygaśnięcia sesji po tekście błędu | Zastąpiono sprawdzeniem HTTP 401. Zmiana tłumaczenia komunikatu nie zmienia już działania programu. |
| Ponowne pobieranie tokena natychmiast po wylogowaniu | Usunięto. Nowy anonimowy token jest pobierany dopiero przy następnej próbie logowania. |
| Powtarzane try/catch/finally przy zapisie, usuwaniu i wylogowaniu | Zastąpiono jedną funkcją `runAction`, która blokuje powtórne operacje i obsługuje błędy. |
| Wielokrotne tworzenie formatera dat | Zastąpiono jednym `Intl.DateTimeFormat` w module widoku. |
| Ukryty podgląd przechowujący treść po wylogowaniu | Czyszczony razem z listą, edytorem i hasłem. |
| Porównywanie modyfikowalnego UpdatedAt w teście zgodności baz | Test porównuje Id, CreatedAt, tytuł i treść. Własny test edycji zmienia UpdatedAt, co wcześniej utrudniało ponowne wykonanie testów bez resetu. |
| Długie linie HTML/CSS/JavaScript | Sformatowano do zwykłej edycji; nie wprowadzono bundlera ani zależności npm. |

## Co pozostawiono celowo

| Fragment | Dlaczego jest potrzebny |
| --- | --- |
| Dwa niezależne projekty | Różnice bezpieczeństwa są widoczne w odpowiadających sobie plikach. Nie ukryto podatności we wspólnym przełączniku uruchomieniowym. |
| Stałe listenery loopback i sprawdzanie lokalnej bazy | Ograniczają zasięg podatnego laboratorium i chronią przed resetem niewłaściwej bazy. |
| Oddzielne cookies i nazwy aplikacji Data Protection | Rozdzielają sesje i chronione tokeny wariantów. |
| `AddControllersWithViews` w zabezpieczonym wariancie | Dostarcza również usługi wymagane przez standardowy filtr `AutoValidateAntiforgeryTokenAttribute`; nie oznacza używania Razor do frontendu. |
| Endpoint tokena CSRF w obu wariantach | Zachowuje taki sam kontrakt klienta i testów. W wariancie podatnym celowo brakuje walidacji, a nie samego tokena. |
| Pobieranie CSRF po zalogowaniu | Token jest związany z tożsamością; token anonimowy nie zastępuje tokena użytkownika. |
| Licznik wersji odpowiedzi wyszukiwania | Chroni przed nadpisaniem nowych wyników wolniejszą wcześniejszą odpowiedzią oraz przed przywróceniem listy po wylogowaniu. |
| Blokada równoległych zapisów i ostrzeganie o niezapisanych zmianach | Zapobiegają przypadkowym duplikatom i utracie wpisanej treści. |
| Walidacja kontraktów i ograniczenia mapowania EF | Pierwsza sprawdza żądanie HTTP, drugie ustalają schemat bazy. Nie zastępują się nawzajem. |
| `AsNoTracking` przy odczycie | Nie tworzy zbędnego stanu śledzenia encji dla listy i podglądu. |
| Transakcja tworzenia konta i fikcyjnych notatek | Zapobiega pozostawieniu połowicznie zainicjalizowanego konta po błędzie. |
| Migracja, snapshot, plik projektanta i pakiet EF Design | Są potrzebne do odtworzenia bazy i dalszej pracy z migracjami. To nie jest zbędny kod aplikacyjny. |
| Surowy SQL i `innerHTML` w wersji podatnej | Są przedmiotem ćwiczeń; zostały wyraźnie opisane jako celowe podatności. |

Nie dodano repozytoriów, osobnej warstwy serwisów ani interfejsu do każdej klasy. Przy tak małym CRUD zwiększałyby liczbę przejść po plikach bez uproszczenia logiki.

## Weryfikacja

Po zmianach: kompilacja Release bez błędów i ostrzeżeń, brak zmian modelu poza migracjami, 66/66 asercji API na prawdziwym PostgreSQL. W przeglądarce ponownie sprawdzono celowe wykonanie XSS w podatnym wariancie i bezpieczne wyświetlenie tekstu w zabezpieczonym, a także zapis, edycję i wyszukiwanie w frontendzie. Szczegóły w `TEST-RESULTS.md`.
