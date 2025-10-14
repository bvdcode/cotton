namespace Cotton.Server.Validators
{
    public static class NameValidator
    {
        // full path - 1024 bytes in UTF-8
        // segment - 255 bytes in UTF-8

        /*
            * Запрещённые символы в имени

        NUL (U+0000), все управляющие U+0001..U+001F, U+007F..U+009F.

        Слэши и бэкслэши: / и \.

        Windows-зарезервированное: < > : " | ? * и двоеточие : вообще.

        Запрет на имя ровно "." и "..".

        Запрет на завершающий пробел и точку: имя не может оканчиваться на или ..

        Зарезервированные имена (Windows, без расширений, регистронезависимо)

        CON, PRN, AUX, NUL, CLOCK$.

        COM1..COM9, LPT1..LPT9.

        Эти строки запрещай как голые имена, независимо от регистра.

        Нормализация

        Приводи к Unicode NFC.

        Для уникальности в папке используй регистронезависимое сравнение: индекс по Unicode case-fold (не только ToLower).

        Обрезай ведущие и хвостовые пробелы, хвостовые точки. Внутренние пробелы не трогай.

        Дополнительно на твой вкус

        Запрет невидимых zero-width символов: U+200B, U+200C, U+200D, U+2060 и т. п. чтобы не ловить визуальные коллизии.

        Ограничь число графем в имени, например ≤128, чтобы не злоупотребляли комбинируемыми знаками.
        */

        public static bool IsValidName(string name, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = "Name cannot be empty or whitespace.";
                return false;
            }
            if (name.Length < 1)
            {
                errorMessage = "Name must be at least 1 character long.";
                return false;
            }
            if (name.Length > 255)
            {
                // TODO: Validate the size of ANSI vs Unicode characters    
                errorMessage = "Name cannot exceed 255 characters.";
                return false;
            }


            errorMessage = string.Empty;
            return true;
        }
    }
}
