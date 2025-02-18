using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using NUnit.Framework;

namespace TemplateInterpreter.Tests
{
    [TestFixture]
    public class InterpreterTests
    {
        private Interpreter _interpreter;
        private Lexer _lexer;

        [SetUp]
        public void Setup()
        {
            _interpreter = new Interpreter();
            _lexer = new Lexer();
        }

        [Test]
        public void ComplexArithmetic()
        {
            // Arrange
            var template = "Result: {{((5 * (var2 + 1.5)) / 2) - 1}}";
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var2", 2.5);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Result: 9.0"));
        }

        [Test]
        public void ComplexBooleanLogic()
        {
            // Arrange
            var template = "{{!(var1 == \"test\" && (var2 > 2 || var3.x < 0))}}";
            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", "test");
            dataDict.Add("var2", 2.5);
            var nested = new ExpandoObject();
            ((IDictionary<string, object>)nested).Add("x", 1.5);
            dataDict.Add("var3", nested);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("False"));
        }

        [Test]
        public void NestedIfStatements()
        {
            // Arrange
            var template = @"
            {{#if var1 == ""test""}}
                Outer if true
                {{#if var2 * 2 >= 5}}
                    Inner if also true
                {{#else}}
                    Inner if false
                {{/if}}
            {{#else}}
                Outer if false
            {{/if}}";
            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", "test");
            dataDict.Add("var2", 2.5);

            // Act
            var result = _interpreter.Interpret(template, data).Trim();

            // Assert
            StringAssert.Contains("Outer if true", result);
            StringAssert.Contains("Inner if also true", result);
        }

        [Test]
        public void EachStatementWithComplexCondition()
        {
            // Arrange
            var template = @"
            Users with high scores:
            {{#for user in users}}
                {{#if user.score > 80 && (user.age >= 18 || user.region == ""EU"")}}
                    {{user.name}} ({{user.score}})
                {{/if}}
            {{/for}}";

            var data = new ExpandoObject();
            var usersList = new List<ExpandoObject>();

            var user1 = new ExpandoObject();
            var user1Dict = (IDictionary<string, object>)user1;
            user1Dict.Add("name", "Alice");
            user1Dict.Add("score", 90);
            user1Dict.Add("age", 18);
            user1Dict.Add("region", "EU");
            usersList.Add(user1);

            var user2 = new ExpandoObject();
            var user2Dict = (IDictionary<string, object>)user2;
            user2Dict.Add("name", "Bob");
            user2Dict.Add("score", 85);
            user2Dict.Add("age", 25);
            user2Dict.Add("region", "EU");
            usersList.Add(user2);

            ((IDictionary<string, object>)data).Add("users", usersList);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("Alice (90)", result);
            StringAssert.Contains("Bob (85)", result);
        }

        [Test]
        public void ComplexMathAndLogicExpressions()
        {
            // Arrange
            var template = @"
            {{ (var1 * 2 + var2) / var3.x }}
            {{ ((var1 > var2) == (var3.x < 5)) && !(var2 == 2.5) }}";

            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", 3);
            dataDict.Add("var2", 2.5);
            var nested = new ExpandoObject();
            ((IDictionary<string, object>)nested).Add("x", 2);
            dataDict.Add("var3", nested);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("4.25", result);
            StringAssert.Contains("False", result);
        }

        [Test]
        public void NestedEachStatementsWithConditionals()
        {
            // Arrange
            var template = @"
            {{#for department in departments}}
            Department: {{department.name}}
                {{#for employee in department.employees}}
                    {{#if employee.salary > department.avgSalary && !employee.isTemp}}
                        {{employee.name}} (Senior)
                    {{#elseif employee.salary > department.avgSalary * 0.8}}
                        {{employee.name}} (Mid-level)
                    {{#else}}
                        {{employee.name}} (Junior)
                    {{/if}}
                {{/for}}
            {{/for}}";

            var data = new ExpandoObject();
            var deptList = new List<ExpandoObject>();

            var itDept = new ExpandoObject();
            var itDeptDict = (IDictionary<string, object>)itDept;
            itDeptDict.Add("name", "IT");
            itDeptDict.Add("avgSalary", 75000);

            var itEmployees = new List<ExpandoObject>();
            var emp1 = new ExpandoObject();
            ((IDictionary<string, object>)emp1).Add("name", "John");
            ((IDictionary<string, object>)emp1).Add("salary", 80000);
            ((IDictionary<string, object>)emp1).Add("isTemp", false);
            itEmployees.Add(emp1);

            var emp2 = new ExpandoObject();
            ((IDictionary<string, object>)emp2).Add("name", "Jane");
            ((IDictionary<string, object>)emp2).Add("salary", 65000);
            ((IDictionary<string, object>)emp2).Add("isTemp", false);
            itEmployees.Add(emp2);

            itDeptDict.Add("employees", itEmployees);
            deptList.Add(itDept);
            ((IDictionary<string, object>)data).Add("departments", deptList);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("Department: IT", result);
            StringAssert.Contains("John (Senior)", result);
            StringAssert.Contains("Jane (Mid-level)", result);
        }

        [Test]
        public void ArbitrarilyDeepVariablePathReference()
        {
            // Arrange
            var template = "{{var1.foo.bar.baz}}";
            var data = new ExpandoObject();
            var nested1 = new ExpandoObject();
            var nested2 = new ExpandoObject();
            var nested3 = new ExpandoObject();
            ((IDictionary<string, object>)nested3).Add("baz", "hello world");
            ((IDictionary<string, object>)nested2).Add("bar", nested3);
            ((IDictionary<string, object>)nested1).Add("foo", nested2);
            ((IDictionary<string, object>)data).Add("var1", nested1);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [Test]
        public void ArrayLength()
        {
            // Arrange
            var template = "{{length(myArray)}}";
            var data = new ExpandoObject();
            var employees = new List<ExpandoObject>();

            var emp1 = new ExpandoObject();
            ((IDictionary<string, object>)emp1).Add("name", "John");
            ((IDictionary<string, object>)emp1).Add("salary", 80000);
            employees.Add(emp1);

            var emp2 = new ExpandoObject();
            ((IDictionary<string, object>)emp2).Add("name", "Jane");
            ((IDictionary<string, object>)emp2).Add("salary", 65000);
            employees.Add(emp2);

            ((IDictionary<string, object>)data).Add("myArray", employees);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void NestedFunctionCalls()
        {
            // Arrange
            var template = "{{concat(\"hello\", concat(\" \", \"world\"))}}";
            var data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [Test]
        public void StringFunctions()
        {
            // Arrange
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var1", "Hello World");

            // Act & Assert
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"World\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{startsWith(\"Hello World\", \"Hello\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{endsWith(\"Hello World\", \"World\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{toUpper(\"Hello World\")}}", data), Is.EqualTo("HELLO WORLD"));
            Assert.That(_interpreter.Interpret("{{toLower(\"Hello World\")}}", data), Is.EqualTo("hello world"));
            Assert.That(_interpreter.Interpret("{{trim(\"  Hello World  \")}}", data), Is.EqualTo("Hello World"));
            Assert.That(_interpreter.Interpret("{{indexOf(\"Hello World\", \"World\")}}", data), Is.EqualTo("6"));
            Assert.That(_interpreter.Interpret("{{lastIndexOf(\"Hello World World\", \"World\")}}", data), Is.EqualTo("12"));
            Assert.That(_interpreter.Interpret("{{substring(\"Hello World\", 6)}}", data), Is.EqualTo("World"));
            Assert.That(_interpreter.Interpret("{{substring(\"Hello World\", 0, 5)}}", data), Is.EqualTo("Hello"));
            Assert.That(_interpreter.Interpret("{{substring(var1, indexOf(var1, \"W\"))}}", data), Is.EqualTo("World"));
        }

        [Test]
        public void LambdaFunction()
        {
            // Arrange
            var template = @"{{#for user in filter(users, (x) => x.age > 17 && length(filter(x.loc, (x) => x.name == ""Atlanta"")) > 0)}}{{user.age}}{{#for loc in user.loc}}{{loc.name}}{{/for}}{{/for}}";
            var data = new ExpandoObject();
            var users = new List<ExpandoObject>();

            var emp1 = new ExpandoObject();
            var locs1 = new List<ExpandoObject>();
            var loc1a = new ExpandoObject();
            var loc1b = new ExpandoObject();
            ((IDictionary<string, object>)loc1a).Add("name", "Atlanta");
            ((IDictionary<string, object>)loc1b).Add("name", "Denver");
            locs1.Add(loc1a);
            locs1.Add(loc1b);
            ((IDictionary<string, object>)emp1).Add("age", "17");
            ((IDictionary<string, object>)emp1).Add("loc", locs1);
            users.Add(emp1);

            var emp2 = new ExpandoObject();
            var locs2 = new List<ExpandoObject>();
            var loc2a = new ExpandoObject();
            var loc2b = new ExpandoObject();
            ((IDictionary<string, object>)loc2a).Add("name", "Atlanta");
            ((IDictionary<string, object>)loc2b).Add("name", "Decatur");
            locs2.Add(loc2a);
            locs2.Add(loc2b);
            ((IDictionary<string, object>)emp2).Add("age", "21");
            ((IDictionary<string, object>)emp2).Add("loc", locs2);
            users.Add(emp2);

            ((IDictionary<string, object>)data).Add("users", users);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("21AtlantaDecatur"));
        }

        [Test]
        public void ObjectOperations()
        {
            // Arrange & Act
            var result1 = _interpreter.Interpret("{{obj(name: \"John\", age: 30).name}}", new ExpandoObject());
            var result2 = _interpreter.Interpret("{{obj(person: obj(name: \"John\", age: 30), active: true).person.age}}", new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("John"));
            Assert.That(result2, Is.EqualTo("30"));
        }

        [Test]
        public void DateTimeFunctions()
        {
            // Arrange
            var template = @"
                {{ format(datetime(""2024-01-08 10:10:10""), ""yyyy-MM-dd HH:mm:ss"") }}
                {{ format(addYears(datetime(""2024-01-08 10:10:10""), 1), ""yyyy-MM-dd"") }}
                {{ format(addMonths(datetime(""2024-01-08 10:10:10""), 6), ""yyyy-MM-dd"") }}
                {{ format(addDays(datetime(""2024-01-08 10:10:10""), 10), ""yyyy-MM-dd"") }}";

            // Act
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            StringAssert.Contains("2024-01-08 10:10:10", result);
            StringAssert.Contains("2025-01-08", result);
            StringAssert.Contains("2024-07-08", result);
            StringAssert.Contains("2024-01-18", result);
        }

        [Test]
        public void ArrayOperations()
        {
            // Arrange
            var templates = new Dictionary<string, string>
            {
                {"emptyArray", "{{#for x in []}}{{x}}, {{/for}}"},
                {"numericArray", "{{#for x in [1.1, 2, 0.3]}}{{x}}, {{/for}}"},
                {"stringArray", "{{#for x in [\"hello world\", \"foo bar\"]}}{{x}}, {{/for}}"},
                {"objectArray", "{{#for x in [obj(name: \"Jeff\"), obj(name: \"Jim\")]}}{{x.name}}, {{/for}}"},
                {"mixedArray", "{{#for x in [\"foo\", 2, \"bar\", false, obj(x: 1, y: 2)]}}{{x}}, {{/for}}"},
                {"nestedArray", "{{#for x in [1, [2, 3], 4]}}{{x}}, {{/for}}"},
                {"objectPropertyArray", "{{#for x in obj(arr: [1, 2, 3]).arr}}{{x}}, {{/for}}"}
            };

            // Act & Assert
            Assert.That(_interpreter.Interpret(templates["emptyArray"], new ExpandoObject()), Is.EqualTo(""));
            Assert.That(_interpreter.Interpret(templates["numericArray"], new ExpandoObject()), Is.EqualTo("1.1, 2, 0.3, "));
            Assert.That(_interpreter.Interpret(templates["stringArray"], new ExpandoObject()), Is.EqualTo("hello world, foo bar, "));
            Assert.That(_interpreter.Interpret(templates["objectArray"], new ExpandoObject()), Is.EqualTo("Jeff, Jim, "));
            Assert.That(_interpreter.Interpret(templates["mixedArray"], new ExpandoObject()), Is.EqualTo("foo, 2, bar, False, System.Dynamic.ExpandoObject, "));
            Assert.That(_interpreter.Interpret(templates["nestedArray"], new ExpandoObject()), Is.EqualTo("1, System.Collections.Generic.List`1[System.Object], 4, "));
            Assert.That(_interpreter.Interpret(templates["objectPropertyArray"], new ExpandoObject()), Is.EqualTo("1, 2, 3, "));
        }

        [Test]
        public void ContainsFunction()
        {
            // Arrange
            var data = new ExpandoObject();
            var user = new { firstName = "John", lastName = "Doe" };
            dynamic person = new ExpandoObject();
            person.name = "John";
            var dict = new Dictionary<string, object> { ["key"] = "value" };

            ((IDictionary<string, object>)data).Add("user", user);
            ((IDictionary<string, object>)data).Add("person", person);
            ((IDictionary<string, object>)data).Add("dict", dict);

            // Act & Assert
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"World\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"foo\")}}", data), Is.EqualTo("False"));
            Assert.That(_interpreter.Interpret("{{contains(user, \"firstName\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{contains(user, \"age\")}}", data), Is.EqualTo("False"));
            Assert.That(_interpreter.Interpret("{{contains(person, \"name\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{contains(person, \"age\")}}", data), Is.EqualTo("False"));
            Assert.That(_interpreter.Interpret("{{contains(dict, \"key\")}}", data), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{contains(dict, \"missing\")}}", data), Is.EqualTo("False"));
        }

        [Test]
        public void CollectionOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{at([1, 2, 3], 1)}}", new ExpandoObject()), Is.EqualTo("2"));
            Assert.That(_interpreter.Interpret("{{first([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("1"));
            Assert.That(_interpreter.Interpret("{{last([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{any([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{any([])}}", new ExpandoObject()), Is.EqualTo("False"));
            Assert.That(_interpreter.Interpret("{{join([3.4, false, \"foo\"], \" | \")}}", new ExpandoObject()), Is.EqualTo("3.4 | False | foo"));
            Assert.That(_interpreter.Interpret("{{#for x in explode(\"a,b,c\", \",\")}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("a b c "));
            Assert.That(_interpreter.Interpret("{{#for x in map([1, 2, 3], (x) => x * 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("2 4 6 "));
            Assert.That(_interpreter.Interpret("{{reduce([1, 2, 3, 4], (acc, curr) => acc + curr, 0)}}", new ExpandoObject()), Is.EqualTo("10"));
            Assert.That(_interpreter.Interpret("{{#for x in take([\"foo\", \"bar\", \"baz\"], 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("foo bar "));
            Assert.That(_interpreter.Interpret("{{#for x in skip([\"foo\", \"bar\", \"baz\"], 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("baz "));
        }

        [Test]
        public void OrderingOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{#for x in order([4, 7, 2])}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("2 4 7 "));
            Assert.That(_interpreter.Interpret("{{#for x in order([4, 7, 2], false)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("7 4 2 "));
            Assert.That(_interpreter.Interpret("{{#for x in order([\"aaaa\", \"zz\", \"yyy\"], ((a, b) => length(a) - length(b)))}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("zz yyy aaaa "));
        }

        [Test]
        public void ObjectAndGroupOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{get(obj(name: \"gordon\", age: 22), \"name\")}}", new ExpandoObject()), Is.EqualTo("gordon"));
            Assert.That(_interpreter.Interpret("{{#for x in keys(obj(name: \"John\", age: 30, city: \"Atlanta\"))}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("name age city "));

            var groupTemplate = "{{#for key in keys(group([" +
                "obj(city: \"Atlanta\", name: \"Jeff\", age: 10), " +
                "obj(city: \"Atlanta\", name: \"Jim\", age: 44), " +
                "obj(city: \"Denver\", name: \"Cindy\", age: 23)], " +
                "\"city\"))}}{{key}} {{/for}}";
            Assert.That(_interpreter.Interpret(groupTemplate, new ExpandoObject()), Is.EqualTo("Atlanta Denver "));
        }

        [Test]
        public void NumericOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{mod(7, 3)}}", new ExpandoObject()), Is.EqualTo("1"));
            Assert.That(_interpreter.Interpret("{{floor(3.7)}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{ceil(3.2)}}", new ExpandoObject()), Is.EqualTo("4"));
            Assert.That(_interpreter.Interpret("{{round(3.45)}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{round(3.45678, 2)}}", new ExpandoObject()), Is.EqualTo("3.46"));
        }

        [Test]
        public void ConversionOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{string(123.45)}}", new ExpandoObject()), Is.EqualTo("123.45"));
            Assert.That(_interpreter.Interpret("{{string(true)}}", new ExpandoObject()), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{number(\"123.45\")}}", new ExpandoObject()), Is.EqualTo("123.45"));
            Assert.That(_interpreter.Interpret("{{numeric(\"123.45\")}}", new ExpandoObject()), Is.EqualTo("True"));
            Assert.That(_interpreter.Interpret("{{numeric(\"abc\")}}", new ExpandoObject()), Is.EqualTo("False"));
        }

        [Test]
        public void LambdaAndHigherOrderFunctions()
        {
            // Arrange
            var templates = new Dictionary<string, string>
            {
                {"square", "{{((x) => x * x)(5)}}"},
                {"regularFunction", "{{concat(\"Hello \", \"World\")}}"},
                {"simpleLambda", "{{((a, b) => a + b)(2, 3)}}"},
                {"higherOrder1", "{{((f) => f(2))((x) => x * 3)}}"},
                {"higherOrder2", "{{((f) => f(\"Hello\", \"World\"))((a, b) => concat(a, b))}}"},
                {"higherOrder3", "{{((f) => ((g) => f(g(\"hello\", \"world\"))))((a) => toUpper(a))((x, y) => concat(x, y))}}"}
            };

            // Act & Assert
            Assert.That(_interpreter.Interpret(templates["square"], new ExpandoObject()), Is.EqualTo("25"));
            Assert.That(_interpreter.Interpret(templates["regularFunction"], new ExpandoObject()), Is.EqualTo("Hello World"));
            Assert.That(_interpreter.Interpret(templates["simpleLambda"], new ExpandoObject()), Is.EqualTo("5"));
            Assert.That(_interpreter.Interpret(templates["higherOrder1"], new ExpandoObject()), Is.EqualTo("6"));
            Assert.That(_interpreter.Interpret(templates["higherOrder2"], new ExpandoObject()), Is.EqualTo("HelloWorld"));
            Assert.That(_interpreter.Interpret(templates["higherOrder3"], new ExpandoObject()), Is.EqualTo("HELLOWORLD"));
        }

        [Test]
        public void IncludeTemplateTest()
        {
            // Arrange
            var registry = new TemplateRegistry();
            registry.RegisterTemplate("header", "<h1>{{title}}</h1>");
            registry.RegisterTemplate("footer", "<footer>{{copyright}}</footer>");

            var interpreter = new Interpreter(registry);

            var template = "{{#include header}}<main>{{content}}</main>{{#include footer}}";

            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("title", "My Page");
            ((IDictionary<string, object>)data).Add("content", "Hello World");
            ((IDictionary<string, object>)data).Add("copyright", "© 2025");

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("<h1>My Page</h1><main>Hello World</main><footer>© 2025</footer>"));
        }

        [Test]
        public void NestedIncludesTest()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("outer", @"
<div>
    <h1>{{title}}</h1>
    {{#include inner}}
</div>");

            registry.RegisterTemplate("inner", @"
<section>
    {{message}}
    {{#include footer}}
</section>");

            registry.RegisterTemplate("footer", @"
<footer>
    {{copyright}}
</footer>");

            // Create test data using ExpandoObject
            dynamic data = new ExpandoObject();
            data.title = "Welcome";
            data.message = "Hello World";
            data.copyright = "© 2025";

            var template = "{{#include outer}}";

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("<h1>Welcome</h1>", result);
            StringAssert.Contains("Hello World", result);
            StringAssert.Contains("© 2025", result);

            // Verify proper nesting
            Assert.That(result.IndexOf("<div>") < result.IndexOf("<section>"),
                "Outer div should contain section");
            Assert.That(result.IndexOf("<section>") < result.IndexOf("<footer>"),
                "Section should contain footer");
            Assert.That(result.IndexOf("</footer>") < result.IndexOf("</section>"),
                "Footer should close before section");
            Assert.That(result.IndexOf("</section>") < result.IndexOf("</div>"),
                "Section should close before div");
        }

        [Test]
        public void IncludeInsideEach()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("list", @"
<ul>
    {{#for item in items}}
        {{#include listitem}}
    {{/for}}
</ul>");

            registry.RegisterTemplate("listitem", @"
<li class=""item"">
    <span class=""name"">{{item.name}}</span>
    <span class=""value"">{{item.value}}</span>
</li>");

            // Create test data using ExpandoObject and List<dynamic>
            var items = new List<dynamic>();

            dynamic item1 = new ExpandoObject();
            item1.name = "First";
            item1.value = 100;
            items.Add(item1);

            dynamic item2 = new ExpandoObject();
            item2.name = "Second";
            item2.value = 200;
            items.Add(item2);

            dynamic data = new ExpandoObject();
            data.items = items;

            var template = "{{#include list}}";

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("<span class=\"name\">First</span>", result);
            StringAssert.Contains("<span class=\"value\">100</span>", result);
            StringAssert.Contains("<span class=\"name\">Second</span>", result);
            StringAssert.Contains("<span class=\"value\">200</span>", result);

            // Verify order and nesting
            Assert.That(result.IndexOf("First") < result.IndexOf("Second"),
                "First item should appear before second item");
            Assert.That(result.IndexOf("<ul>") < result.IndexOf("<li"),
                "List items should be inside ul");
            Assert.That(result.IndexOf("</li>") < result.IndexOf("</ul>"),
                "List items should close before ul closes");
        }

        [Test]
        public void IncludeInsideIf_RendersCorrectly()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("profile", @"
<div class=""profile"">
    <h2>{{username}}</h2>
    {{#if premium}}
        {{#include premiumcontent}}
    {{#else}}
        {{#include basiccontent}}
    {{/if}}
</div>");

            registry.RegisterTemplate("premiumcontent", @"
<div class=""premium"">
    <span class=""badge"">Premium User</span>
    <p>{{specialMessage}}</p>
</div>");

            registry.RegisterTemplate("basiccontent", @"
<div class=""basic"">
    <p>Upgrade to premium!</p>
</div>");

            // Create test data using ExpandoObject
            dynamic premiumUser = new ExpandoObject();
            premiumUser.username = "JohnDoe";
            premiumUser.premium = true;
            premiumUser.specialMessage = "Welcome to premium!";

            dynamic basicUser = new ExpandoObject();
            basicUser.username = "JaneSmith";
            basicUser.premium = false;

            // Test with premium user
            var premiumResult = interpreter.Interpret("{{#include profile}}", premiumUser);

            // Test with basic user
            var basicResult = interpreter.Interpret("{{#include profile}}", basicUser);

            // Assert premium user result
            StringAssert.Contains("<h2>JohnDoe</h2>", premiumResult);
            StringAssert.Contains("<span class=\"badge\">Premium User</span>", premiumResult);
            StringAssert.Contains("Welcome to premium!", premiumResult);
            StringAssert.DoesNotContain("Upgrade to premium!", premiumResult);

            // Assert basic user result
            StringAssert.Contains("<h2>JaneSmith</h2>", basicResult);
            StringAssert.Contains("Upgrade to premium!", basicResult);
            StringAssert.DoesNotContain("Premium User", basicResult);
            StringAssert.DoesNotContain("Welcome to premium!", basicResult);

            // Verify proper nesting
            Assert.That(premiumResult.IndexOf("<div class=\"profile\">") < premiumResult.IndexOf("<div class=\"premium\">"),
                "Premium content should be nested inside profile div");
            Assert.That(basicResult.IndexOf("<div class=\"profile\">") < basicResult.IndexOf("<div class=\"basic\">"),
                "Basic content should be nested inside profile div");
        }

        [Test]
        public void BasicVariableAssignment()
        {
            // Template that assigns a value and then outputs it
            var template = "{{#let x = 2}}{{x}}";

            // Create empty data context
            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void VariableExpressionAssignment()
        {
            // Template that uses a variable in an expression to assign to another variable
            var template = "{{#let x = 2}}{{#let y = x + 12}}{{y}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("14"));
        }

        [Test]
        public void LambdaFunctionAssignment()
        {
            // Template that defines a lambda function, assigns variables, and uses them
            var template = @"
                {{#let f = (a, b) => a * b}}
                {{#let x = 2}}
                {{#let y = 14}}
                {{#let z = f(x, y)}}
                {{z}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("28"));
        }

        [Test]
        public void VariableRedefinitionThrowsException()
        {
            var template = "{{#let x = 2}}{{#let x = 3}}";
            dynamic data = new ExpandoObject();

            Assert.Throws<Exception>(() => _interpreter.Interpret(template, data),
                "Should throw exception when trying to redefine variable");
        }

        [Test]
        public void VariableConflictWithDataContextThrowsException()
        {
            var template = "{{#let existingField = 2}}";

            dynamic data = new ExpandoObject();
            var dict = (IDictionary<string, object>)data;
            dict["existingField"] = 1;

            Assert.Throws<Exception>(() => _interpreter.Interpret(template, data),
                "Should throw exception when variable name conflicts with data context");
        }

        [Test]
        public void VariableConflictWithIteratorThrowsException()
        {
            var template = @"
                {{#for item in [1,2,3]}}
                    {{#let item = 2}}
                {{/for}}";

            dynamic data = new ExpandoObject();

            Assert.Throws<Exception>(() => _interpreter.Interpret(template, data),
                "Should throw exception when variable name conflicts with iterator");
        }

        [Test]
        public void VariableScopeInNestedStructures()
        {
            var template = @"{{#let x = 1}}{{#for item in [1,2]}}{{#let y = x + item}}{{y}}{{/for}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("23")); // Should output "2" then "3"
        }

        [Test]
        public void ComplexExpressionAssignment()
        {
            var template = @"
                {{#let x = 10}}
                {{#let y = 5}}
                {{#let z = (x * y) + (x / y)}}
                {{z}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("52")); // (10 * 5) + (10 / 5) = 50 + 2 = 52
        }

        [Test]
        public void LambdaAccessToVariables()
        {
            // Template that defines a variable and then uses it within a lambda
            var template = @"{{#let x = 2}}{{((a) => a * x)(3)}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("6")); // 3 * 2 = 6
        }

        [Test]
        public void LambdaClosures()
        {
            // Template that defines a variable and then uses it within a lambda
            var template = @"{{#let x = 2}}{{#let f = ((a) => (() => a * x))}}{{f(3)()}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("6")); // 3 * 2 = 6
        }

        [Test]
        public void IteratorVariableNameConflicts()
        {
            // Test case 1: Iterator conflicts with existing variable
            var template1 = @"
            {{#let item = 5}}
            {{#for item in [1,2,3]}}
                {{item}}
            {{/for}}";

            dynamic data = new ExpandoObject();

            Assert.Throws<Exception>(() => _interpreter.Interpret(template1, data),
                "Should throw exception when iterator name conflicts with existing variable");

            // Test case 2: Variable conflicts with existing iterator
            var template2 = @"
            {{#for item in [1,2,3]}}
                {{#let item = 5}}
                {{item}}
            {{/for}}";

            Assert.Throws<Exception>(() => _interpreter.Interpret(template2, data),
                "Should throw exception when variable name conflicts with existing iterator");

            // Test case 3: Verify proper nested loops with different iterator names work
            var template3 = @"{{#for i in [1,2]}}{{#for j in [3,4]}}{{i * j}}{{/for}}{{/for}}";

            var result = _interpreter.Interpret(template3, data).Trim();
            Assert.That(result, Is.EqualTo("3468")); // Should output: 3,4,6,8
        }

        [Test]
        public void SingleLineComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "Hello {{* This is a comment *}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(2));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello "));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[1].Value, Is.EqualTo(" World"));
        }

        [Test]
        public void MultiLineComment_ShouldBeIgnored()
        {
            // Arrange
            var template = @"Hello {{* 
                This is a
                multi-line comment
            *}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(2));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello "));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[1].Value, Is.EqualTo(" World"));
        }

        [Test]
        public void CommentBetweenDirectives_ShouldBeIgnored()
        {
            // Arrange
            var template = "{{ value1 }}{{* comment *}}{{ value2 }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(6));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void NestedDirectiveLikeSyntaxInComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "{{* This {{ should }} be ignored *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void UnterminatedComment_ShouldThrowException()
        {
            // Arrange
            var template = "Hello {{* This comment is not terminated";

            // Act & Assert
            var ex = Assert.Throws<Exception>(() => _lexer.Tokenize(template));
            Assert.That(ex.Message, Is.EqualTo("Unterminated comment"));
        }

        [Test]
        public void MultipleConsecutiveComments_ShouldAllBeIgnored()
        {
            // Arrange
            var template = "{{* Comment 1 *}}{{* Comment 2 *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void CommentWithAsterisksInContent_ShouldBeParsedCorrectly()
        {
            // Arrange
            var template = "{{* This * has * asterisks * in * it *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void EmptyComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "Hello {{**}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(2));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello "));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[1].Value, Is.EqualTo(" World"));
        }

        [Test]
        public void StringQuotes_CanBeEscaped()
        {
            // Arrange
            var template = "{{#let x = \"\\\"hello world\\\"\"}}{{x}}";

            // Act
            dynamic data = new ExpandoObject();

            // Assert
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("\"hello world\""));
        }

        [Test]
        public void SingleQuote()
        {
            var tokens = _lexer.Tokenize("{{\"Hello 'World'\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello 'World'"));
        }

        [Test]
        public void DoubleQuoteEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello \\\"World\\\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello \"World\""));
        }

        [Test]
        public void BackslashEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello \\\\World\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello \\World"));
        }

        [Test]
        public void NewlineEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\nWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\nWorld"));
        }

        [Test]
        public void CarriageReturnEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\rWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\rWorld"));
        }

        [Test]
        public void TabEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\tWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\tWorld"));
        }

        [Test]
        public void MultipleEscapeSequences()
        {
            var tokens = _lexer.Tokenize("{{\"\\\"Hello\\n\\tWorld\\\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("\"Hello\n\tWorld\""));
        }

        [Test]
        public void InvalidEscapeSequence()
        {
            Assert.Throws<System.Exception>(() => 
                _lexer.Tokenize("{{\"Hello\\xWorld\"}}")
            );
        }

        [Test]
        public void UnterminatedString()
        {
            Assert.Throws<System.Exception>(() => 
                _lexer.Tokenize("{{\"Hello World")
            );
        }

        [Test]
        public void EscapeAtEndOfString()
        {
            Assert.Throws<System.Exception>(() => 
                _lexer.Tokenize("{{\"Hello World\\")
            );
        }

        [Test]
        public void EmptyString()
        {
            var tokens = _lexer.Tokenize("{{\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo(""));
        }

        [Test]
        public void StringWithOnlyEscapeSequence()
        {
            var tokens = _lexer.Tokenize("{{\"\\n\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
            
            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("\n"));
        }
    }
}
