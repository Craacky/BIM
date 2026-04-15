USE [BIM_DB_Test];
GO

DELETE FROM [Products];
GO

INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901210', N'Молоко 3.2% 1л');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901227', N'Творог 9% 200г');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901234', N'Масло сливочное 82.5% 180г');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901241', N'Сметана 20% 400г');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901258', N'Сыр Российский 1кг');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901265', N'Йогурт Клубника 125г');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901272', N'Кефир 1% 1л');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901289', N'Ряженка 2.5% 500г');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901296', N'Сливки 10% 200мл');
INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('12345678901203', N'Говядина тушеная 338г');
GO
