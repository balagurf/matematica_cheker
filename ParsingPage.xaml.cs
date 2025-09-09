using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GrammarChecker
{
    public partial class ParsingPage : Page
    {
        // Правила грамматики
        private readonly Dictionary<string, List<string>> grammar = new Dictionary<string, List<string>>
        {
            { "S", new List<string> { "AB" } },
            { "A", new List<string> { "Ca", "Ba" } },
            { "B", new List<string> { "Cb", "b" } },
            { "C", new List<string> { "cb", "" } } // ε представлено пустой строкой
        };

        public ParsingPage()
        {
            InitializeComponent();
        }

        private void CheckString_Click(object sender, RoutedEventArgs e)
        {
            string input = InputString.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ResultText.Text = "Введите строку!";
                return;
            }

            var sb = new StringBuilder();
            bool belongs = Parse("S", input, sb, 0);

            ResultText.Text = belongs ? "Строка принадлежит языку ✅" : "Строка не принадлежит языку ❌";
            ParseTreeOutput.Text = sb.ToString();
        }

        private bool Parse(string nonterminal, string input, StringBuilder sb, int indent)
        {
            string pad = new string(' ', indent * 2);
            sb.AppendLine($"{pad}{nonterminal} ⇒ {input}");

            // если пустой символ (ε)
            if (grammar.ContainsKey(nonterminal))
            {
                foreach (var production in grammar[nonterminal])
                {
                    sb.AppendLine($"{pad}Пробуем правило: {nonterminal} → {production}");

                    string expanded = production;
                    if (expanded == "") expanded = ""; // ε

                    // пробуем построить рекурсивно
                    if (TryExpand(expanded, input, sb, indent + 1))
                        return true;
                }
            }

            return false;
        }

        private bool TryExpand(string production, string input, StringBuilder sb, int indent)
        {
            if (production == input) return true;

            if (production.Length == 0 && input.Length == 0) return true;

            // идём по символам
            for (int i = 0; i < production.Length; i++)
            {
                char symbol = production[i];
                if (char.IsUpper(symbol)) // нетерминал
                {
                    string before = production.Substring(0, i);
                    string after = production.Substring(i + 1);

                    foreach (var rule in grammar[symbol.ToString()])
                    {
                        string newProd = before + rule + after;
                        if (TryExpand(newProd, input, sb, indent + 1))
                        {
                            sb.AppendLine(new string(' ', indent * 2) +
                                $"Заменили {symbol} → {rule}, получаем: {newProd}");
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
