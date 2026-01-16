using System.Text.RegularExpressions;

namespace CodeQualityChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter the full path to the C# file (.cs) you want to analyze:");
            string filePath = Console.ReadLine()?.Trim('"');

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("Error: File not found.");
                return;
            }

            var analyzer = new CodeAnalyzer(filePath);
            var issues = analyzer.RunChecks();

            Console.WriteLine("\n--- Analysis Report ---\n");

            if (issues.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("All requirements are met!");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var issue in issues)
                {
                    Console.WriteLine($"- [Failed] {issue}");
                }
            }
            Console.ResetColor();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    public class CodeAnalyzer
    {
        private readonly string[] _lines;
        private readonly string _fullText;
        private readonly List<string> _errors;

        // Regex helpers
        private readonly Regex _methodSignature = new Regex(@"^\s*(public|private|protected|internal)\s+(static\s+)?[\w<>[\]]+\s+\w+\s*\(.*\)", RegexOptions.Compiled);
        private readonly Regex _classField = new Regex(@"^\s*(public|private|protected|internal)\s+(const\s+|static\s+|readonly\s+)*[\w<>[\]]+\s+\w+(\s*=.*)?;", RegexOptions.Compiled);
        private readonly Regex _mediaTypes = new Regex(@"(Bitmap|Image|SoundPlayer|Video|Animation|Texture)", RegexOptions.Compiled);

        public CodeAnalyzer(string path)
        {
            _lines = File.ReadAllLines(path);
            _fullText = File.ReadAllText(path);
            _errors = new List<string>();
        }

        public List<string> RunChecks()
        {
            Check1_Styling();
            Check2_VisibilityAndStatic();
            Check3_AttributesAndMedia();
            Check4_DRY();
            Check5_ArraysOrLists();
            Check6_Loops();
            Check7_MagicNumbers();
            Check8_SubroutineSpacing();
            Check9_Documentation();
            Check10_FunctionExists();

            return _errors;
        }

        // 1. C# Styling Conventions (Basic checks: PascalCase methods, Brace placement)
        private void Check1_Styling()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i].Trim();
                // Check Method Casing (PascalCase) - very basic heuristic
                if (_methodSignature.IsMatch(_lines[i]))
                {
                    var match = Regex.Match(_lines[i], @"\s+([\w]+)\s*\(");
                    if (match.Success)
                    {
                        string methodName = match.Groups[1].Value;
                        if (!char.IsUpper(methodName[0]) && methodName != "Main") 
                        {
                            _errors.Add($"Rule 1 Violation: Method '{methodName}' on line {i + 1} does not follow PascalCase conventions.");
                        }
                    }
                }
            }
        }

        // 2. Visibility defined, No public static variables
        private void Check2_VisibilityAndStatic()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i].Trim();
                
                // Ignore comments
                if (line.StartsWith("//") || line.StartsWith("/*")) continue;
                // Check for public static fields (variables) that are not const or readonly
                if (line.Contains("public static") && !line.Contains("const") && !line.Contains("readonly") && !line.Contains("("))
                {
                    _errors.Add($"Rule 2 Violation: Public static variable found on line {i + 1}.");
                }

                // Check for missing visibility modifiers on Class level items
                // This is a heuristic: If it starts with a type like 'int x;' or 'void Do()' without public/private
                // excluding local variables inside methods requires knowing if we are inside a method.
                // We will skip strict implementation of "missing visibility" due to parsing complexity, 
                // but we enforce the specific 'public static' rule strictly.
            }
        }

        // 3. Classwide attributes logic (Const, Static Readonly for media)
        private void Check3_AttributesAndMedia()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i].Trim();
                if (_mediaTypes.IsMatch(line) && line.Contains(";")) // It's a declaration
                {
                    if (!line.Contains("static readonly"))
                    {
                        _errors.Add($"Rule 3 Violation: Media attribute on line {i + 1} must be 'static readonly'.");
                    }
                }
            }
        }

        // 4. DRY - Check for repeated blocks of 3+ lines
        private void Check4_DRY()
        {
            // Simple heuristic: Take chunks of 3 lines and see if they repeat exactly elsewhere
            Dictionary<string, int> blockCounts = new Dictionary<string, int>();

            for (int i = 0; i < _lines.Length - 3; i++)
            {
                string block = _lines[i].Trim() + _lines[i+1].Trim() + _lines[i+2].Trim();
                if (string.IsNullOrWhiteSpace(block) || block.Length < 10) continue; // Skip empty blocks or strict braces

                if (blockCounts.ContainsKey(block))
                {
                    // Only flag if it's not just closing braces
                    if(!Regex.IsMatch(block, @"^[{}]+$"))
                    {
                        _errors.Add($"Rule 4 Warning (DRY): Potential repetitive code detected starting at line {i + 1}.");
                        return; // Only report once to avoid spam
                    }
                }
                else
                {
                    blockCounts[block] = 1;
                }
            }
        }

        // 5. At least one Array or List
        private void Check5_ArraysOrLists()
        {
            if (!(_fullText.Contains("[]") || _fullText.Contains("List<") || _fullText.Contains("Array")))
            {
                _errors.Add("Rule 5 Violation: No Array or List found in the code.");
            }
        }

        // 6. At least one loop
        private void Check6_Loops()
        {
            bool hasLoop = Regex.IsMatch(_fullText, @"\b(for|foreach|while|do)\s*\(");
            if (!hasLoop)
            {
                _errors.Add("Rule 6 Violation: No loop (for, foreach, while, do) found.");
            }
        }

        // 7. No useless literals (Magic Numbers)
        private void Check7_MagicNumbers()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i].Trim();
                if (line.StartsWith("//") || line.Contains("const ")) continue;

                // Look for numbers that are not 0, 1, or -1, and are not part of a variable name
                MatchCollection matches = Regex.Matches(line, @"\b\d+\b");
                foreach (Match match in matches)
                {
                    string num = match.Value;
                    if (num != "0" && num != "1" && num != "-1")
                    {
                        // Exclude common array indices or simple math if obvious, but flag generic numbers
                        // verifying context is hard, but we flag if it looks like an assignment or condition
                        if (line.Contains("=") || line.Contains("==") || line.Contains(">") || line.Contains("<"))
                        {
                            _errors.Add($"Rule 7 Violation: Potential magic number '{num}' found on line {i + 1}. Consider using a variable or const.");
                            break; // One per line is enough
                        }
                    }
                }
            }
        }

        // 8. Every subroutine has 2 empty lines afterwards
        private void Check8_SubroutineSpacing()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_methodSignature.IsMatch(_lines[i]))
                {
                    // We found a method start. We need to find its closing brace.
                    int openBraces = 0;
                    bool insideMethod = false;
                    int methodEndIndex = -1;

                    for (int j = i; j < _lines.Length; j++)
                    {
                        string l = _lines[j];
                        int opens = l.Count(c => c == '{');
                        int closes = l.Count(c => c == '}');

                        if (opens > 0) insideMethod = true;
                        openBraces += (opens - closes);

                        if (insideMethod && openBraces == 0)
                        {
                            methodEndIndex = j;
                            break;
                        }
                    }

                    if (methodEndIndex != -1 && methodEndIndex < _lines.Length - 2)
                    {
                        // Check the next two lines
                        string lineAfter1 = _lines[methodEndIndex + 1].Trim();
                        string lineAfter2 = _lines[methodEndIndex + 2].Trim();

                        // We allow the very end of class brace to be immediate
                        if (lineAfter1 != "" || lineAfter2 != "")
                        {
                            // Exception: If the next line is the end of the class '}', it's usually acceptable, 
                            // but strict interpretation of the rule says 2 empty lines.
                            if (lineAfter1 != "}") 
                            {
                                _errors.Add($"Rule 8 Violation: Method ending at line {methodEndIndex + 1} does not have 2 empty lines after it.");
                            }
                        }
                    }
                }
            }
        }

        // 9. Documentation exists
        private void Check9_Documentation()
        {
            // Check classes
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i].Trim();
                
                // Is Class or Method
                if (Regex.IsMatch(line, @"\bclass\b") || _methodSignature.IsMatch(line))
                {
                    // Check previous lines for comments
                    bool hasDoc = false;
                    if (i > 0)
                    {
                        string prev = _lines[i - 1].Trim();
                        if (prev.StartsWith("//") || prev.EndsWith("*/")) hasDoc = true;
                    }
                    
                    if (!hasDoc)
                    {
                        _errors.Add($"Rule 9 Violation: Undocumented Class or Method found on line {i + 1}.");
                    }
                }
            }
        }
        // 10. At least one function
        private void Check10_FunctionExists()
        {
            bool hasMethod = false;
            foreach (var line in _lines)
            {
                if (_methodSignature.IsMatch(line))
                {
                    hasMethod = true;
                    break;
                }
            }

            if (!hasMethod)
            {
                _errors.Add("Rule 10 Violation: No functions/subroutines found in the file.");
            }
        }
    }
}
