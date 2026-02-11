using NUnitLite;

namespace TestProject
{
    class Program
    {
        static int Main(string[] args)
        {
            PrintHeader();

            // 确保有输出目录
            Directory.CreateDirectory("./TestResults");

            // 设置测试运行参数
            var defaultArgs = new List<string>
            {
                "--noheader",
                "--labels=All",
                "--work=./TestResults",
                "--out=TestResult.txt",
                "--err=TestErrors.txt",
                "--result=TestResult.xml;format=nunit3"
            };

            // 如果命令行没有参数，使用默认参数
            if (args.Length == 0)
            {
                args = defaultArgs.ToArray();
            }

            // 初始化测试管理器
            var testManager = new CryptoTestManager();

            // 注册支持的算法测试
            testManager.RegisterAlgorithmTest<AesCryptoTests>("AES", "高级加密标准");
            testManager.RegisterAlgorithmTest<DesCryptoTests>("DES", "数据加密标准");
            // testManager.RegisterAlgorithmTest<RsaCryptoTests>("RSA", "非对称加密");
            // testManager.RegisterAlgorithmTest<BlowfishCryptoTests>("Blowfish", "对称加密算法");

            Console.WriteLine("加载测试程序集...");
            testManager.PrintRegisteredAlgorithms();
            Console.WriteLine();

            // 运行测试
            var startTime = DateTime.Now;
            var result = testManager.RunTests(args);
            var elapsedTime = DateTime.Now - startTime;

            // 解析 XML 结果文件获取详细数据
            ParseAndPrintResults(elapsedTime, testManager);

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();

            return result;
        }

        #region Other methods
        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                CRYPTO LIBRARY TEST SUITE                             ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  Supported Algorithms: AES, DES                                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine($"测试开始时间: {DateTime.Now:yyyy年MM月dd日 HH:mm:ss}");
            Console.WriteLine($"运行环境: {Environment.OSVersion}");
            Console.WriteLine($"CLR 版本: {Environment.Version}");
            Console.WriteLine();
        }

        static void ParseAndPrintResults(TimeSpan elapsedTime, CryptoTestManager testManager)
        {
            var resultFile = "./TestResults/TestResult.xml";

            if (!File.Exists(resultFile))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("警告: 未找到测试结果文件");
                Console.ResetColor();
                return;
            }

            try
            {
                var xmlContent = File.ReadAllText(resultFile);
                var testResults = ParseNUnitXml(xmlContent, testManager);

                PrintSummary(testResults, elapsedTime);
                PrintAlgorithmBreakdown(testResults, testManager);
                PrintDetailedResults(testResults);
                PrintTestCategories(testResults);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"解析测试结果时出错: {ex.Message}");
                Console.ResetColor();
            }
        }

        static TestSuiteResult ParseNUnitXml(string xml, CryptoTestManager testManager)
        {
            var result = new TestSuiteResult();
            var testCaseNodes = xml.Split(new[] { "<test-case" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var node in testCaseNodes.Skip(1))
            {
                var testCase = ParseTestCaseNode(node);
                result.TestCases.Add(testCase);
                result.TotalCount++;

                // 统计结果
                switch (testCase.Result)
                {
                    case TestResult.Passed:
                        result.PassedCount++;
                        break;
                    case TestResult.Failed:
                        result.FailedCount++;
                        break;
                    case TestResult.Skipped:
                        result.SkippedCount++;
                        break;
                }

                // 使用测试管理器识别算法
                testCase.Algorithm = testManager.IdentifyAlgorithm(testCase.FullName);
                result.IncrementAlgorithmCount(testCase.Algorithm);
            }

            return result;
        }

        static TestCaseResult ParseTestCaseNode(string node)
        {
            var testCase = new TestCaseResult();

            // 提取测试名称
            testCase.FullName = ExtractAttribute(node, "fullname=\"", "\"");

            // 提取结果
            if (node.Contains("result=\"Passed\""))
            {
                testCase.Result = TestResult.Passed;
            }
            else if (node.Contains("result=\"Failed\""))
            {
                testCase.Result = TestResult.Failed;
                testCase.Message = ExtractCData(node, "<message><![CDATA[", "]]></message>");
                testCase.StackTrace = ExtractCData(node, "<stack-trace><![CDATA[", "]]></stack-trace>");
            }
            else if (node.Contains("result=\"Skipped\""))
            {
                testCase.Result = TestResult.Skipped;
            }

            // 提取持续时间
            var durationStr = ExtractAttribute(node, "duration=\"", "\"");
            if (double.TryParse(durationStr, out double duration))
                testCase.Duration = TimeSpan.FromSeconds(duration);

            // 提取类别
            testCase.Category = ExtractProperty(node, "Category");

            return testCase;
        }

        static string ExtractAttribute(string text, string startMarker, string endMarker)
        {
            var startIndex = text.IndexOf(startMarker);
            if (startIndex == -1) return "";

            startIndex += startMarker.Length;
            var endIndex = text.IndexOf(endMarker, startIndex);

            return endIndex > startIndex ? text.Substring(startIndex, endIndex - startIndex) : "";
        }

        static string ExtractCData(string text, string startMarker, string endMarker)
        {
            var startIndex = text.IndexOf(startMarker);
            if (startIndex == -1) return "";

            startIndex += startMarker.Length;
            var endIndex = text.IndexOf(endMarker, startIndex);

            return endIndex > startIndex ? text.Substring(startIndex, endIndex - startIndex) : "";
        }

        static string ExtractProperty(string text, string propertyName)
        {
            var propertyStart = text.IndexOf($"<property name=\"{propertyName}\"");
            if (propertyStart == -1) return "";

            var valueStart = text.IndexOf("value=\"", propertyStart);
            if (valueStart == -1) return "";

            valueStart += 7;
            var valueEnd = text.IndexOf("\"", valueStart);

            return valueEnd > valueStart ? text.Substring(valueStart, valueEnd - valueStart) : "";
        }

        static void PrintSummary(TestSuiteResult results, TimeSpan elapsedTime)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("测试结果汇总");
            Console.WriteLine(new string('═', 60));
            Console.ResetColor();

            Console.WriteLine($"测试总数:  {results.TotalCount}");

            // 动态显示所有算法的测试数量
            foreach (var algorithm in results.GetAlgorithmStats())
            {
                Console.WriteLine($"{algorithm.Name} 测试:  {algorithm.Count}");
            }

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"通过:      {results.PassedCount}");
            Console.ResetColor();
            Console.WriteLine($"  ({CalculatePercentage(results.PassedCount, results.TotalCount)}%)");

            if (results.FailedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"失败:      {results.FailedCount}");
                Console.ResetColor();
                Console.WriteLine($"  ({CalculatePercentage(results.FailedCount, results.TotalCount)}%)");
            }
            else
            {
                Console.WriteLine($"失败:      {results.FailedCount}");
            }

            if (results.SkippedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"跳过:      {results.SkippedCount}");
                Console.ResetColor();
                Console.WriteLine($"  ({CalculatePercentage(results.SkippedCount, results.TotalCount)}%)");
            }
            else
            {
                Console.WriteLine($"跳过:      {results.SkippedCount}");
            }

            Console.WriteLine($"总耗时:    {elapsedTime.TotalSeconds:F2} 秒");
            Console.WriteLine($"平均耗时:  {(results.TotalCount > 0 ? elapsedTime.TotalSeconds / results.TotalCount : 0):F3} 秒/测试");

            // 显示进度条
            Console.WriteLine();
            DisplayProgressBar(results);
        }

        static void PrintAlgorithmBreakdown(TestSuiteResult results, CryptoTestManager testManager)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("算法测试统计:");
            Console.WriteLine(new string('─', 60));
            Console.ResetColor();

            foreach (var algorithm in results.GetAlgorithmStats())
            {
                var algorithmTests = results.TestCases
                    .Where(t => t.Algorithm == algorithm.Name)
                    .ToList();

                var passed = algorithmTests.Count(t => t.Result == TestResult.Passed);
                var failed = algorithmTests.Count(t => t.Result == TestResult.Failed);
                var skipped = algorithmTests.Count(t => t.Result == TestResult.Skipped);

                var algorithmInfo = testManager.GetAlgorithmInfo(algorithm.Name);
                var displayName = algorithmInfo != null ?
                    $"{algorithm.Name} ({algorithmInfo.Description})" :
                    algorithm.Name;

                Console.WriteLine($"{displayName}:");

                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"✓ 通过: {passed}");
                Console.ResetColor();

                if (failed > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($" ✗ 失败: {failed}");
                    Console.ResetColor();
                }

                if (skipped > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($" ⚡ 跳过: {skipped}");
                    Console.ResetColor();
                }

                Console.WriteLine($"   总计: {algorithmTests.Count}");
            }
        }

        static void DisplayProgressBar(TestSuiteResult results)
        {
            const int barWidth = 50;
            var passedWidth = (int)((double)results.PassedCount / results.TotalCount * barWidth);
            var failedWidth = (int)((double)results.FailedCount / results.TotalCount * barWidth);
            var skippedWidth = barWidth - passedWidth - failedWidth;

            Console.Write("进度: [");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', passedWidth));

            if (failedWidth > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(new string('█', failedWidth));
            }

            if (skippedWidth > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(new string('░', skippedWidth));
            }

            Console.ResetColor();
            Console.WriteLine("]");
        }

        static void PrintDetailedResults(TestSuiteResult results)
        {
            if (results.FailedCount > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("失败测试详情:");
                Console.WriteLine(new string('─', 60));
                Console.ResetColor();

                foreach (var testCase in results.TestCases.Where(t => t.Result == TestResult.Failed))
                {
                    Console.WriteLine($"\n❌ [{testCase.Algorithm}] {testCase.FullName}");
                    if (!string.IsNullOrEmpty(testCase.Message))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"   错误: {testCase.Message}");
                        Console.ResetColor();
                    }

                    if (!string.IsNullOrEmpty(testCase.StackTrace))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        var firstLine = testCase.StackTrace.Split('\n').FirstOrDefault();
                        if (!string.IsNullOrEmpty(firstLine))
                            Console.WriteLine($"   位置: {firstLine.Trim()}");
                        Console.ResetColor();
                    }

                    Console.WriteLine($"   耗时: {testCase.Duration.TotalSeconds:F3} 秒");
                }
            }
        }

        static void PrintTestCategories(TestSuiteResult results)
        {
            var categories = results.TestCases
                .Where(t => !string.IsNullOrEmpty(t.Category))
                .GroupBy(t => t.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Passed = g.Count(t => t.Result == TestResult.Passed),
                    Failed = g.Count(t => t.Result == TestResult.Failed),
                    Skipped = g.Count(t => t.Result == TestResult.Skipped),
                    Algorithms = string.Join(", ", g.Select(t => t.Algorithm).Distinct())
                })
                .OrderBy(g => g.Category)
                .ToList();

            if (categories.Any())
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("测试类别统计:");
                Console.WriteLine(new string('─', 60));
                Console.ResetColor();

                foreach (var category in categories)
                {
                    Console.Write($"  {category.Category,-15} ");
                    Console.Write($"总数: {category.Count,3}  ");

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"✓{category.Passed,2} ");
                    Console.ResetColor();

                    if (category.Failed > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write($"✗{category.Failed,2} ");
                        Console.ResetColor();
                    }

                    if (category.Skipped > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"⚡{category.Skipped,2} ");
                        Console.ResetColor();
                    }

                    Console.Write($"算法: {category.Algorithms}");
                    Console.WriteLine();
                }
            }
        }

        static string CalculatePercentage(int value, int total)
        {
            if (total == 0) return "0.0";
            return ((double)value / total * 100).ToString("F1");
        }
        #endregion
    }

    #region 核心管理类
    /// <summary>
    /// 算法测试信息
    /// </summary>
    public class AlgorithmTestInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Type TestClassType { get; set; }

        public AlgorithmTestInfo(string name, string description, Type testClassType)
        {
            Name = name;
            Description = description;
            TestClassType = testClassType;
        }
    }

    /// <summary>
    /// 加密算法测试管理器
    /// </summary>
    public class CryptoTestManager
    {
        private readonly Dictionary<string, AlgorithmTestInfo> _registeredAlgorithms = new();

        /// <summary>
        /// 注册算法测试类
        /// </summary>
        /// <typeparam name="TTestClass">测试类类型</typeparam>
        /// <param name="algorithmName">算法名称</param>
        /// <param name="description">算法描述</param>
        public void RegisterAlgorithmTest<TTestClass>(string algorithmName, string description)
        {
            _registeredAlgorithms[algorithmName] = new AlgorithmTestInfo(
                algorithmName,
                description,
                typeof(TTestClass)
            );
        }

        /// <summary>
        /// 获取所有已注册的算法信息
        /// </summary>
        public IEnumerable<AlgorithmTestInfo> GetRegisteredAlgorithms()
        {
            return _registeredAlgorithms.Values;
        }

        /// <summary>
        /// 根据测试类全名识别算法
        /// </summary>
        public string IdentifyAlgorithm(string testClassName)
        {
            foreach (var algorithm in _registeredAlgorithms.Values)
            {
                if (testClassName.Contains(algorithm.TestClassType.Name))
                {
                    return algorithm.Name;
                }
            }
            return "Unknown";
        }

        /// <summary>
        /// 获取算法信息
        /// </summary>
        public AlgorithmTestInfo? GetAlgorithmInfo(string algorithmName)
        {
            return _registeredAlgorithms.TryGetValue(algorithmName, out var info) ? info : null;
        }

        /// <summary>
        /// 运行所有测试
        /// </summary>
        public int RunTests(string[] args)
        {
            var currentAssembly = typeof(Program).Assembly;
            var runner = new AutoRun(currentAssembly);
            return runner.Execute(args);
        }

        /// <summary>
        /// 打印已注册的算法信息
        /// </summary>
        public void PrintRegisteredAlgorithms()
        {
            Console.WriteLine($"已注册算法数: {_registeredAlgorithms.Count}");
            foreach (var algorithm in _registeredAlgorithms.Values)
            {
                Console.WriteLine($"  - {algorithm.Name}: {algorithm.Description}");
            }
        }
    }
    #endregion

    #region 结果类
    enum TestResult { Passed, Failed, Skipped }

    class TestCaseResult
    {
        public string FullName { get; set; } = "";
        public TestResult Result { get; set; }
        public TimeSpan Duration { get; set; }
        public string Message { get; set; } = "";
        public string StackTrace { get; set; } = "";
        public string Category { get; set; } = "";
        public string Algorithm { get; set; } = "";
    }

    class TestSuiteResult
    {
        public List<TestCaseResult> TestCases { get; } = new();
        public int TotalCount { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }

        // 使用字典存储算法统计，支持动态添加新算法
        private readonly Dictionary<string, int> _algorithmCounts = new();

        /// <summary>
        /// 增加算法计数
        /// </summary>
        public void IncrementAlgorithmCount(string algorithmName)
        {
            if (string.IsNullOrEmpty(algorithmName) || algorithmName == "Unknown")
                return;

            if (!_algorithmCounts.ContainsKey(algorithmName))
                _algorithmCounts[algorithmName] = 0;

            _algorithmCounts[algorithmName]++;
        }

        /// <summary>
        /// 获取指定算法的测试数量
        /// </summary>
        public int GetAlgorithmCount(string algorithmName)
        {
            return _algorithmCounts.TryGetValue(algorithmName, out var count) ? count : 0;
        }

        /// <summary>
        /// 获取所有算法的统计信息
        /// </summary>
        public IEnumerable<(string Name, int Count)> GetAlgorithmStats()
        {
            return _algorithmCounts.Select(kv => (kv.Key, kv.Value));
        }
    }
    #endregion
}
