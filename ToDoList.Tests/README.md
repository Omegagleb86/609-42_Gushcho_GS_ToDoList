# Проверки ToDoList на MSTest

Запуск из общей папки проектов:

```bash
dotnet test ToDoList.Tests/ToDoList.Tests.csproj
```

Всего 19 независимых тестовых методов:

- `Core/TodoRulesTests.cs` — 2 проверки правил: длина названия и повтор ID задачи.
- `ViewModels/MainViewModelTests.cs` — 10 проверок команд: задачи, категории,
  подзадачи, независимые статусы, переименование, удаление, ошибки хранилища.
- `Data/EfTodoRepositoryTests.cs` — 5 проверок ORM: чтение и сохранение дерева,
  обновление/перенос, удаление, неверная категория и откат всей транзакции при сбое.
- `Data/SqliteSchemaTests.cs` — генерация схемы SQLite.
- `Data/SqliteFilePersistenceTests.cs` — сохранение в файл и чтение через новый
  экземпляр репозитория (проверка сохранности данных между запусками).

Запуск отдельной группы:

```bash
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=Core
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=ViewModels
dotnet test ToDoList.Tests/ToDoList.Tests.csproj --filter TestCategory=Data
```

У каждого теста собственные данные и репозиторий. Большинство тестов ORM открывает
собственную SQLite-базу в памяти и закрывает её через `await using`.
Тест сохранения в файл создаёт уникальный временный каталог и удаляет его после
выполнения. Рабочая база приложения не используется, отдельный сервер не нужен.
Это позволяет сохранить включённое параллельное выполнение MSTest.

Core, Data и Models подключаются через ProjectReference. ViewModels находятся
в MAUI-проекте, но не зависят от MAUI: тестовый проект подключает их исходники через
Compile/Link. Копий исходников нет, запускать приложение для тестов не требуется.
