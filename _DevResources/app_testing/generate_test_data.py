import os

# 1. Настройка путей
base_dir = r"c:\Users\cbxjy\Projects\BIM _net56\BIM_Control\app_testing\Работа с кодами"
if not os.path.exists(base_dir):
    os.makedirs(base_dir)

# 2. Продукты с корректными 14-значными GTIN
products = [
    {"gtin": "12345678901210", "name": "Молоко 3.2% 1л"},
    {"gtin": "12345678901227", "name": "Творог 9% 200г"},
    {"gtin": "12345678901234", "name": "Масло сливочное 82.5% 180г"},
    {"gtin": "12345678901241", "name": "Сметана 20% 400г"},
    {"gtin": "12345678901258", "name": "Сыр Российский 1кг"},
    {"gtin": "12345678901265", "name": "Йогурт Клубника 125г"},
    {"gtin": "12345678901272", "name": "Кефир 1% 1л"},
    {"gtin": "12345678901289", "name": "Ряженка 2.5% 500г"},
    {"gtin": "12345678901296", "name": "Сливки 10% 200мл"},
    {"gtin": "12345678901203", "name": "Говядина тушеная 338г"},
]

def generate_valid_code(gtin, index):
    """
    Генерирует код длиной ровно 31 символ (valid DataMatrix):
    Схема: 01 + GTIN(14) + 21 + Serial(6) + GS + 93 + Crypto(4)
    Индексы:
      0-1:   01
      2-15:  GTIN (12345678901210)
      16-17: 21 (AI Серийного номера)
      18-23: Serial (6 цифр) -> чтобы сумма до GS была 24 символа
      24:    GS (\\u001d)
      25-26: 93 (AI Криптоключа)
      27-30: Crypto (4 знака)
    ИТОГО: 31 символ.
    """
    gs = "\u001d"
    
    # Серийный номер должен быть 6 символов, чтобы GS встал на 24 позицию
    serial = f"{index:06d}" 
    
    # Криптохвост 4 символа
    crypto = f"{(index % 10000):04d}" 

    # Сборка кода
    # 01(2) + GTIN(14) + 21(2) + SER(6) = 24 символа. Следующий (25-й, индекс 24) будет GS.
    code = f"01{gtin}21{serial}{gs}93{crypto}"
    
    return code

# 3. Генерация основного файла NewTest.txt (смешанные коды для теста)
new_test_path = os.path.join(base_dir, "NewTest.txt")
with open(new_test_path, "w", encoding="utf-8") as f:
    for i, p in enumerate(products):
        for j in range(50): # по 50 кодов каждого продукта
            code = generate_valid_code(p["gtin"], i * 1000 + j)
            f.write(code + "\n")
print(f"Сгенерирован {new_test_path} (формат 31 символ)")

# 4. Генерация отдельных файлов
for i, p in enumerate(products):
    file_path = os.path.join(base_dir, f"test_file_{i+1}.txt")
    with open(file_path, "w", encoding="utf-8") as f:
        for j in range(100):
            code = generate_valid_code(p["gtin"], j)
            f.write(code + "\n")
    print(f"Сгенерирован {file_path}")

# 5. SQL скрипт для синхронизации базы
sql_path = r"c:\Users\cbxjy\Projects\BIM _net56\BIM_Control\app_testing\seed_products.sql"
with open(sql_path, "w", encoding="utf-8") as f:
    f.write("USE [BIM_DB_Test];\nGO\n\n")
    f.write("DELETE FROM [Products];\nGO\n\n")
    for p in products:
        f.write(f"INSERT INTO [Products] ([GTIN], [AboutProduct]) VALUES ('{p['gtin']}', N'{p['name']}');\n")
    f.write("GO\n")

print(f"\nSQL скрипт обновлен: {sql_path}")
print("ВАЖНО: Запустите SQL скрипт в базе данных, чтобы GTIN-ы совпадали!")