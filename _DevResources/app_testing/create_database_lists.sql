USE [BIM_DB_Test];
GO

/* 
   Скрипт для ручного пересоздания таблицы DatabaseLists.
   Поля соответствуют сущности DatabaseList.cs в проекте.
*/

IF OBJECT_ID('[DatabaseLists]', 'U') IS NULL
BEGIN
    CREATE TABLE [DatabaseLists] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(MAX) NULL,
        [FirstCode] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME2(7) NOT NULL,
        [Status] INT NOT NULL,
        CONSTRAINT [PK_DatabaseLists] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Таблица DatabaseLists успешно создана.';
END
ELSE
BEGIN
    PRINT 'Таблица DatabaseLists уже существует.';
END
GO
