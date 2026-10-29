# Проверки ToDoList на MSTest

Запуск из общей папки проектов:

```bash
dotnet test ToDoList.Tests/ToDoList.Tests.csproj
```

Все 18 сценариев из прежнего `ToDoList/Tests/Program.cs` перенесены в независимые
тестовые методы. Общего последовательного сценария и консольного запуска больше нет.

- `Core/TodoRulesTests.cs` — 2 проверки правил: длина названия и повтор ID задачи.
- `ViewModels/MainViewModelTests.cs` — 10 проверок команд: задачи, категории,
  подзадачи, независимые статусы, переименование, удаление, ошибки хранилища.
- `Data/EfTodoRepositoryTests.cs` — 5 проверок ORM: чтение и сохранение дерева,
  обновление/перенос, удаление, неверная категория и откат всей транзакции при сбое.
- `Data/MySqlSchemaTests.cs` — генерация схемы MySQL без подключения к серверу.

Запуск отдельной группы:

```bash
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=Core
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=ViewModels
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=Data
```

У каждого теста собственные данные и репозиторий. Каждый тест ORM открывает
собственную SQLite-базу в памяти, закрывает её через `await using` и не использует
рабочую базу MySQL, пароль или переменную `TODOLIST_MYSQL_CONNECTION`.
Это позволяет сохранить включённое параллельное выполнение MSTest.
Тест генерации MySQL-схемы не заменяет интеграционные проверки на реальном MySQL.

Core, Data и Models подключаются через ProjectReference. ViewModels пока находятся
в MAUI-проекте, но не зависят от MAUI: тестовый проект подключает их исходники через
Compile/Link (как делал прежний проект проверок). Копий исходников нет, переносить
ViewModels или запускать приложение для этих тестов не требуется.
