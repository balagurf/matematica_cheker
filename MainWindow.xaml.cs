using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace GrammarChecker
{
    public partial class MainWindow : Window
    {
        private const string DefaultStartSymbol = "S";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            RulesInput.Clear();
            DetailsOutput.Clear();
            StatusText.Text = "";
        }

        private void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            var lines = RulesInput.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("Вставьте правила (по одному в строке). Пример: S->aAB", "Нет правил", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string log;
            int type = DetectGrammarType(lines, DefaultStartSymbol, out log);

            string typeName;
            switch (type)
            {
                case 3: typeName = "Тип 3 (Регулярная)"; break;
                case 2: typeName = "Тип 2 (Контекстно-вольная)"; break;
                case 1: typeName = "Тип 1 (Контекстно-зависимая)"; break;
                default: typeName = "Тип 0 (Без ограничений)"; break;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Подробный разбор правил ===");
            sb.AppendLine();
            sb.Append(log);
            sb.AppendLine();
            sb.AppendLine("=== Итог ===");
            sb.AppendLine($"Определённый тип: {typeName}");
            sb.AppendLine();
            sb.AppendLine("Краткое объяснение:");
            if (type == 3)
            {
                sb.AppendLine("- Все правила имеют вид A -> x или A -> xB (или ε), где A — одиночный нетерминал, B — (возможный) одиночный нетерминал, x — цепочка терминалов. Это регулярная грамматика.");
            }
            else if (type == 2)
            {
                sb.AppendLine("- Все правила имеют вид A -> α, где A — одиночный нетерминал, α — любая (возможно пустая) цепочка терминалов и нетерминалов. Это контекстно-свободная грамматика.");
            }
            else if (type == 1)
            {
                sb.AppendLine("- Все правила удовлетворяют условию |α| <= |β| (правая часть не короче левой) (с учётом правила S->ε при отсутствии S в правых частях). Это контекстно-зависимая грамматика.");
            }
            else
            {
                sb.AppendLine("- Правила не соответствуют ни одному из вышеперечисленных классов — тип 0 (без ограничений).");
            }

            DetailsOutput.Text = sb.ToString();
            StatusText.Text = typeName;
        }

        // Основной анализатор: возвращает тип и подробный лог
        private int DetectGrammarType(List<string> rawRules, string startSymbol, out string log)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Стартовый символ: {startSymbol}");
            sb.AppendLine($"Всего правил: {rawRules.Count}");
            sb.AppendLine();

            bool isRegularCandidate = true;
            bool isRightLinear = true;
            bool isLeftLinear = true;
            bool isContextFree = true;
            bool isContextSensitive = true;

            bool hasSEpsilon = false;
            bool sAppearsInRhs = false;

            int ruleIndex = 0;
            foreach (var raw in rawRules)
            {
                ruleIndex++;
                // Поддерживаем '->' или '→'
                var m = Regex.Match(raw, @"^\s*(.+?)\s*(?:->|→)\s*(.*)$");
                if (!m.Success)
                {
                    sb.AppendLine($"Правило {ruleIndex}: Неверный формат: '{raw}' (ожидается 'A->B'). Пропускаю.");
                    // считаем это за некорректное правило для дальнейшей классификации
                    isRegularCandidate = false;
                    isContextFree = false;
                    isContextSensitive = false;
                    continue;
                }

                string left = m.Groups[1].Value.Trim();
                string rightRaw = m.Groups[2].Value.Trim();

                // Обрабатываем epsilon-псевдонимы
                string right;
                if (string.IsNullOrEmpty(rightRaw) ||
                    rightRaw == "λ" || rightRaw == "Λ" ||
                    rightRaw.Equals("eps", StringComparison.OrdinalIgnoreCase) ||
                    rightRaw.Equals("epsilon", StringComparison.OrdinalIgnoreCase) ||
                    rightRaw == "ε")
                {
                    right = ""; // epsilon представляем как пустую строку
                }
                else
                {
                    right = rightRaw;
                }

                sb.AppendLine($"Правило {ruleIndex}: '{left}' -> '{(right == "" ? "ε" : right)}'");

                // Проверки и подсчёты
                int leftLen = left.Length;
                int rightLen = right.Length;

                int leftUpperCount = left.Count(c => char.IsUpper(c));
                int rightUpperCount = right.Count(c => char.IsUpper(c));

                bool leftIsSingleNonterminal = (leftLen == 1 && char.IsUpper(left[0]));

                sb.AppendLine($"  - Длина левой части: {leftLen}. Кол-во заглавных в левой: {leftUpperCount}. " +
                              $"(Левая — одиночный нетерминал? {(leftIsSingleNonterminal ? "Да" : "Нет")})");
                sb.AppendLine($"  - Длина правой части: {rightLen}. Кол-во заглавных в правой: {rightUpperCount}.");

                // Признак CF: левая часть должна быть одиночный нетерм. Если хоть одно правило нарушает — не CF
                if (!leftIsSingleNonterminal) isContextFree = false;

                // Регулярность:
                // - левая часть одиночный нет. (это уже проверил для CF)
                // - правая часть содержит не более одного нетерминала,
                //   и если есть единственный нетерминал — он должен стоять в начале (left-linear) или в конце (right-linear).
                if (right == "")
                {
                    // epsilon допустим (обычно только если LHS == S, но для классификации будем допускать epsilon как возможный, 
                    // но регулярность сохранится только если лев. часть одиночный нет.)
                    // оставляем флаги как есть
                }
                else
                {
                    if (rightUpperCount > 1)
                    {
                        isRegularCandidate = false;
                        sb.AppendLine("  - Регулярность: нарушено (в правой части более одного нетерминала).");
                    }
                    else if (rightUpperCount == 1)
                    {
                        // индекс первого заглавного в правой части
                        int idx = -1;
                        for (int i = 0; i < right.Length; i++) if (char.IsUpper(right[i])) { idx = i; break; }

                        if (idx != right.Length - 1) // не на конце => не праволинейное
                        {
                            isRightLinear = false;
                            sb.AppendLine("  - Праволинейный критерий: НЕ выполняется (нетерминал не на конце).");
                        }
                        else
                        {
                            sb.AppendLine("  - Праволинейный критерий: выполняется (если лев. часть одиночный нетерминал).");
                        }

                        if (idx != 0) // не на начале => не леволинейное
                        {
                            isLeftLinear = false;
                            sb.AppendLine("  - Леволинейный критерий: НЕ выполняется (нетерминал не в начале).");
                        }
                        else
                        {
                            sb.AppendLine("  - Леволинейный критерий: выполняется (если лев. часть одиночный нетерминал).");
                        }
                    }
                    else // rightUpperCount == 0
                    {
                        sb.AppendLine("  - Правая часть — только терминалы (или пустая) → подходит для регулярной формы.");
                    }
                }

                // Контекстно-зависимая проверка (условие длины): |RHS| >= |LHS| (за исключением S->ε в особом случае)
                if (right == "" && left == startSymbol)
                {
                    hasSEpsilon = true;
                    sb.AppendLine("  - Правило S->ε обнаружено (временно допускается, если S не встречается в правых частях других правил).");
                }
                else
                {
                    if (rightLen < leftLen)
                    {
                        isContextSensitive = false;
                        sb.AppendLine("  - Контекстно-зависимая проверка: НЕ выполняется (правая короче левой).");
                    }
                    else
                    {
                        sb.AppendLine("  - Контекстно-зависимая проверка: выполняется (|RHS| >= |LHS|).");
                    }
                }

                // фиксируем, встречается ли S в правых частях (нужно для правила S->ε)
                if (right.Contains(startSymbol)) sAppearsInRhs = true;

                sb.AppendLine();
            } // конец перебора правил

            // Пост-обработка для S->ε
            if (hasSEpsilon && sAppearsInRhs)
            {
                // правило S->ε допустимо ТОЛЬКО если S не встречается в правых частях
                isContextSensitive = false;
                sb.AppendLine("Присутствует S->ε, но S встречается в правых частях → это нарушает условие для контекстно-зависимой грамматики.");
            }

            // Для регулярности необходимо также, чтобы все левые части были одиночными нетерминалами (CF условие)
            if (!isContextFree)
            {
                isRegularCandidate = false;
                sb.AppendLine("Грамматика не контекстно-свободная (есть левые части длины > 1), значит она не может быть регулярной.");
            }
            else
            {
                // Дополнительно: регулярной считается, если правила соответствуют леволинейной ИЛИ праволинейной схеме.
                if (!(isRightLinear || isLeftLinear))
                {
                    isRegularCandidate = false;
                    sb.AppendLine("Правила не соответствуют ни праволинейной, ни леволинейной системе → не регулярна.");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Резюме по булевым флагам:");
            sb.AppendLine($"  isRegularCandidate = {isRegularCandidate}");
            sb.AppendLine($"  isContextFree = {isContextFree}");
            sb.AppendLine($"  isContextSensitive = {isContextSensitive}");
            sb.AppendLine();

            // Выбираем наиболее строгий (наименьший номер типа по Хомскому: 3,2,1,0)
            if (isRegularCandidate) { log = sb.ToString(); return 3; }
            if (isContextFree) { log = sb.ToString(); return 2; }
            if (isContextSensitive) { log = sb.ToString(); return 1; }

            log = sb.ToString();
            return 0;
        }
    }
}
