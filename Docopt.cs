using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace DocoptNet
{

    internal class Argument: LeafPattern
    {
        public Argument(string name, ValueObject value = null) : base(name, value)
        {
        }

        public Argument(string name, string value)
            : base(name, new ValueObject(value))
        {
        }

        public Argument(string name, ICollection coll)
            : base(name, new ValueObject(coll))
        {
        }

        public Argument(string name, int value)
            : base(name, new ValueObject(value))
        {
        }

        public override SingleMatchResult SingleMatch(IList<Pattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] is Argument)
                    return new SingleMatchResult(i, new Argument(Name, left[i].Value));
            }
            return new SingleMatchResult();
        }

        public override Node ToNode()
        {
            return new ArgumentNode(this.Name, (this.Value != null && this.Value.IsList) ? ValueType.List : ValueType.String);
        }

        public override string GenerateCode()
        {
            var s = Name.Replace("<", "").Replace(">", " ").ToLowerInvariant();
            s = "Arg" + GenerateCodeHelper.ConvertDashesToCamelCase(s);

            if (Value != null && Value.IsList)
            {
                return string.Format("public ArrayList {0} {{ get {{ return _args[\"{1}\"].AsList; }} }}", s, Name);
            }
            return string.Format("public string {0} {{ get {{ return null == _args[\"{1}\"] ? null : _args[\"{1}\"].ToString(); }} }}", s, Name);
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DocoptAliasAttribute
        : Attribute
    {
        public DocoptAliasAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("empty string is not allowed.");
            }

            this.Name = name;
        }

        public string Name { get; private set; }
    }

    internal class Binder<T> where T: new()
    {
        // Bind populates the public properties of a given class T with matching option values.
        // Each key in args will be mapped to an public property of class T pointed
        // to by 'container', as follows:
        //
        //   private int Abc {get; set;}  // ignored
        //
        //   public int Abc {get; set;}  // mapped from "--abc", "<abc>", or "abc"
        //                               // (case insensitive)
        //   public int A {get; set;}    // mapped from "-a", "<a>", or "a"
        //                               // (case insensitive)
        //   [DocoptMember(Name="Alias")]
        //   public int A {get; set;}    // mapped from "--alias", "<alias>", or "alias"
        //                               // (case insensitive)
        //
        // Bind also handles conversion to bool, float, double, decimal, int, long or string types.


        public Binder()
        { }

        public T Bind(IDictionary<string, ValueObject> args)
        {
            var options = args.ToDictionary(x => x.Key.Trim(new[] { '-', '<', '>' }).ToLower(), x => x.Value);
            var container = new T();
            var t = container.GetType();

            // Listing only public properties.
            var properties = t.GetProperties();

            foreach (var prop in properties)
            {
                if (!prop.CanWrite) continue;

                ValueObject value = null;

                if (Attribute.IsDefined(prop, typeof(DocoptAliasAttribute)))
                {
                    var attrs = prop.GetCustomAttributes(typeof(DocoptAliasAttribute), true)
                        .Cast<DocoptAliasAttribute>();
                    foreach (var attr in attrs)
                    {
                        options.TryGetValue(attr.Name.ToLower(), out value);
                    }
                }

                if (value == null)
                {
                    options.TryGetValue(prop.Name.ToLower(), out value);
                }

                if (value != null)
                {
                    Set(prop, value, container);
                }
            }

            return container;
        }
        
        private static void Set(PropertyInfo prop, ValueObject value, object container)
        {

            var t = prop.PropertyType;
            var indexParams = prop.GetIndexParameters();

            if (t.IsArray)
            {
                var values = value.AsList;
                var vtype = t.GetElementType();
                var array = Array.CreateInstance(vtype, values.Count);

                for (int i = 0; i < array.Length; ++i)
                {
                    array.SetValue(Convert.ChangeType(ConvertValue(vtype, values[i]), vtype), i);
                }

                prop.SetValue(container, array, null);
            }
            else
            {
                prop.SetValue(container, ConvertValue(t, value.Value), null);
            }
        }

        private static object ConvertValue(Type t, object value)
        {
            if (t == typeof(bool))
            {
                if (value is bool)
                {
                    return value;
                }
                return bool.Parse(value.ToString());
            }

            if (t == typeof(int))
            {
                if (value is int)
                {
                    return value;
                }
                return int.Parse(value.ToString());
            }

            if (t == typeof(long))
            {
                if (value is long)
                {
                    return value;
                }
                return long.Parse(value.ToString());
            }

            if (t == typeof(string))
            {
                if (value is string)
                {
                    return value;
                }
                return value.ToString();
            }

            if (t == typeof(double))
            {
                if (value is double)
                {
                    return value;
                }
                return double.Parse(value.ToString());
            }

            if (t == typeof(float))
            {
                if (value is float)
                {
                    return value;
                }
                return float.Parse(value.ToString());
            }

            if (t == typeof(decimal))
            {
                if (value is decimal)
                {
                    return value;
                }
                return decimal.Parse(value.ToString());
            }

            return value;
        }
    }

    /// <summary>
    ///     Branch/inner node of a pattern tree.
    /// </summary>
    internal class BranchPattern : Pattern
    {

        public BranchPattern(params Pattern[] children)
        {
            if (children == null) throw new ArgumentNullException("children");
            Children = children;
        }

        public override bool HasChildren { get { return true; } }

        public IEnumerable<Pattern> Flat<T>() where T: Pattern
        {
            return Flat(typeof (T));
        }

        public override ICollection<Pattern> Flat(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException("types");
            if (types.Contains(this.GetType()))
            {
                return new Pattern[] { this };
            }
            return Children.SelectMany(child => child.Flat(types)).ToList();
        }

        public override string ToString()
        {
            return string.Format("{0}({1})", GetType().Name, String.Join(", ", Children.Select(c => c == null ? "None" : c.ToString())));
        }
    }

    internal class Command : Argument
    {
        public Command(string name, ValueObject value = null) : base(name, value ?? new ValueObject(false))
        {
        }

        public override SingleMatchResult SingleMatch(IList<Pattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                var pattern = left[i];
                if (pattern is Argument)
                {
                    if (pattern.Value.ToString() == Name)
                        return new SingleMatchResult(i, new Command(Name, new ValueObject(true)));
                    break;
                }
            }
            return new SingleMatchResult();
        }

        public override Node ToNode() { return new CommandNode(this.Name); }

        public override string GenerateCode()
        {
            var s = Name.ToLowerInvariant();
            s = "Cmd" + GenerateCodeHelper.ConvertDashesToCamelCase(s);
            return string.Format("public bool {0} {{ get {{ return _args[\"{1}\"].IsTrue; }} }}", s, Name);
        }

    }

    public class Docopt
    {
        public event EventHandler<PrintExitEventArgs> PrintExit;

        #region Apply
        public IDictionary<string, ValueObject> Apply(string doc)
        {
            return Apply(doc, new Tokens("", typeof (DocoptInputErrorException)));
        }

        public IDictionary<string, ValueObject> Apply(string doc, string cmdLine, bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false)
        {
            return Apply(doc, new Tokens(cmdLine, typeof (DocoptInputErrorException)), help, version, optionsFirst, exit);
        }

        public IDictionary<string, ValueObject> Apply(string doc, ICollection<string> argv, bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false)
        {
            return Apply(doc, new Tokens(argv, typeof (DocoptInputErrorException)), help, version, optionsFirst, exit);
        }
        #endregion

        #region Bind
        public T Bind<T>(string doc) where T : new()
        {
            var args = Apply(doc);
            return new Binder<T>().Bind(args);
        }

        public T Bind<T>(string doc, string cmdLine, bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false) where T : new()
        {
            var args = Apply(doc, cmdLine, help, version, optionsFirst, exit);
            return new Binder<T>().Bind(args);
        }

        public T Bind<T>(string doc, ICollection<string> argv, bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false) where T : new()
        {
            var args = Apply(doc, argv, help, version, optionsFirst, exit);
            return new Binder<T>().Bind(args);
        }
        #endregion

        protected IDictionary<string, ValueObject> Apply(string doc, Tokens tokens,
            bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false)
        {
            try
            {
                SetDefaultPrintExitHandlerIfNecessary(exit);
                var usageSections = ParseSection("usage:", doc);
                if (usageSections.Length == 0)
                    throw new DocoptLanguageErrorException("\"usage:\" (case-insensitive) not found.");
                if (usageSections.Length > 1)
                    throw new DocoptLanguageErrorException("More that one \"usage:\" (case-insensitive).");
                var exitUsage = usageSections[0];
                var options = ParseDefaults(doc);
                var pattern = ParsePattern(FormalUsage(exitUsage), options);
                var arguments = ParseArgv(tokens, options, optionsFirst);
                var patternOptions = pattern.Flat<Option>().Distinct().ToList();
                // [default] syntax for argument is disabled
                foreach (OptionsShortcut optionsShortcut in pattern.Flat(typeof (OptionsShortcut)))
                {
                    var docOptions = ParseDefaults(doc);
                    optionsShortcut.Children = docOptions.Distinct().Except(patternOptions).ToList();
                }
                Extras(help, version, arguments, doc);
                var res = pattern.Fix().Match(arguments);
                if (res.Matched && res.LeftIsEmpty)
                {
                    var dict = new Dictionary<string, ValueObject>();
                    foreach (var p in pattern.Flat())
                    {
                        dict[p.Name] = p.Value;
                    }
                    foreach (var p in res.Collected)
                    {
                        dict[p.Name] = p.Value;
                    }
                    return dict;
                }
                throw new DocoptInputErrorException(exitUsage);
            }
            catch (DocoptBaseException e)
            {
                if (!exit)
                    throw;

                OnPrintExit(e.Message, e.ErrorCode);

                return null;
            }
        }

        private void SetDefaultPrintExitHandlerIfNecessary(bool exit)
        {
            if (exit && PrintExit == null)
                // Default behaviour is to print usage
                // and exit with error code 1
                PrintExit += (sender, args) =>
                {
                    Console.WriteLine(args.Message);
                    Environment.Exit(args.ErrorCode);
                };
        }

        public string GenerateCode(string doc)
        {
            var res = GetFlatPatterns(doc);
            res = res
                .GroupBy(pattern => pattern.Name)
                .Select(group => group.First());
            var sb = new StringBuilder();
            foreach (var p in res)
            {
                sb.AppendLine(p.GenerateCode());
            }
            return sb.ToString();
        }

        public IEnumerable<Node> GetNodes(string doc)
        {
            return GetFlatPatterns(doc)
                .Select(p => p.ToNode())
                .Where(p => p != null)
                .ToArray();
        }

        static IEnumerable<Pattern> GetFlatPatterns(string doc)
        {
            var usageSections = ParseSection("usage:", doc);
            if (usageSections.Length == 0)
                throw new DocoptLanguageErrorException("\"usage:\" (case-insensitive) not found.");
            if (usageSections.Length > 1)
                throw new DocoptLanguageErrorException("More that one \"usage:\" (case-insensitive).");
            var exitUsage = usageSections[0];
            var options = ParseDefaults(doc);
            var pattern = ParsePattern(FormalUsage(exitUsage), options);
            var patternOptions = pattern.Flat<Option>().Distinct().ToList();
            // [default] syntax for argument is disabled
            foreach (OptionsShortcut optionsShortcut in pattern.Flat(typeof (OptionsShortcut)))
            {
                var docOptions = ParseDefaults(doc);
                optionsShortcut.Children = docOptions.Distinct().Except(patternOptions).ToList();
            }
            return pattern.Fix().Flat();
        }

        private void Extras(bool help, object version, ICollection<Pattern> options, string doc)
        {
            if (help && options.Any(o => (o.Name == "-h" || o.Name == "--help") && !o.Value.IsNullOrEmpty))
            {
                OnPrintExit(doc);
            }
            if (version != null && options.Any(o => (o.Name == "--version") && !o.Value.IsNullOrEmpty))
            {
                OnPrintExit(version.ToString());
            }
        }

        protected void OnPrintExit(string doc, int errorCode = 0)
        {
            if (PrintExit == null)
            {
                throw new DocoptExitException(doc);
            }
            else
            {
                PrintExit(this, new PrintExitEventArgs(doc, errorCode));
            }
        }

        /// <summary>
        ///     Parse command-line argument vector.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="options"></param>
        /// <param name="optionsFirst"></param>
        /// <returns></returns>
        internal static IList<Pattern> ParseArgv(Tokens tokens, ICollection<Option> options,
            bool optionsFirst = false)
        {
            //    If options_first:
            //        argv ::= [ long | shorts ]* [ argument ]* [ '--' [ argument ]* ] ;
            //    else:
            //        argv ::= [ long | shorts | argument ]* [ '--' [ argument ]* ] ;

            var parsed = new List<Pattern>();
            while (tokens.Current() != null)
            {
                if (tokens.Current() == "--")
                {
                    parsed.AddRange(tokens.Select(v => new Argument(null, new ValueObject(v))));
                    return parsed;
                }

                if (tokens.Current().StartsWith("--"))
                {
                    parsed.AddRange(ParseLong(tokens, options));
                }
                else if (tokens.Current().StartsWith("-") && tokens.Current() != "-")
                {
                    parsed.AddRange(ParseShorts(tokens, options));
                }
                else if (optionsFirst)
                {
                    parsed.AddRange(tokens.Select(v => new Argument(null, new ValueObject(v))));
                    return parsed;
                }
                else
                {
                    parsed.Add(new Argument(null, new ValueObject(tokens.Move())));
                }
            }
            return parsed;
        }

        internal static string FormalUsage(string exitUsage)
        {
            var section = new StringPartition(exitUsage, ":").RightString; // drop "usage:"
            var pu = section.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            var join = new StringBuilder();
            join.Append("( ");
            for (int i = 1; i < pu.Length; i++)
            {
                var s = pu[i];
                if (i > 1) join.Append(" ");
                join.Append((s == pu[0]) ? ") | (" : s);
            }
            join.Append(" )");
            return join.ToString();
        }

        internal static Required ParsePattern(string source, ICollection<Option> options)
        {
            var tokens = Tokens.FromPattern(source);
            var result = ParseExpr(tokens, options);
            if (tokens.Current() != null)
                throw tokens.CreateException("unexpected ending: " + String.Join(" ", tokens.ToArray()));
            return new Required(result.ToArray());
        }

        private static IEnumerable<Pattern> ParseExpr(Tokens tokens, ICollection<Option> options)
        {
            // expr ::= seq ( '|' seq )* ;
            var seq = ParseSeq(tokens, options);
            if (tokens.Current() != "|")
                return seq;
            var result = new List<Pattern>();
            if (seq.Count() > 1)
            {
                result.Add(new Required(seq.ToArray()));
            }
            else
            {
                result.AddRange(seq);
            }
            while (tokens.Current() == "|")
            {
                tokens.Move();
                seq = ParseSeq(tokens, options);
                if (seq.Count() > 1)
                {
                    result.Add(new Required(seq.ToArray()));
                }
                else
                {
                    result.AddRange(seq);
                }
            }
            result = result.Distinct().ToList();
            if (result.Count > 1)
                return new[] {new Either(result.ToArray())};
            return result;
        }

        private static ICollection<Pattern> ParseSeq(Tokens tokens, ICollection<Option> options)
        {
            // seq ::= ( atom [ '...' ] )* ;
            var result = new List<Pattern>();
            while (!new[] {null, "]", ")", "|"}.Contains(tokens.Current()))
            {
                var atom = ParseAtom(tokens, options);
                if (tokens.Current() == "...")
                {
                    result.Add(new OneOrMore(atom.ToArray()));
                    tokens.Move();
                    return result;
                }
                result.AddRange(atom);
            }
            return result;
        }

        private static IEnumerable<Pattern> ParseAtom(Tokens tokens, ICollection<Option> options)
        {
            // atom ::= '(' expr ')' | '[' expr ']' | 'options'
            //  | long | shorts | argument | command ;            

            var token = tokens.Current();
            var result = new List<Pattern>();
            switch (token)
            {
                case "[":
                case "(":
                {
                    tokens.Move();
                    string matching;
                    if (token == "(")
                    {
                        matching = ")";
                        result.Add(new Required(ParseExpr(tokens, options).ToArray()));
                    }
                    else
                    {
                        matching = "]";
                        result.Add(new Optional(ParseExpr(tokens, options).ToArray()));
                    }
                    if (tokens.Move() != matching)
                        throw tokens.CreateException("unmatched '" + token + "'");
                }
                    break;
                case "options":
                    tokens.Move();
                    result.Add(new OptionsShortcut());
                    break;
                default:
                    if (token.StartsWith("--") && token != "--")
                    {
                        return ParseLong(tokens, options);
                    }
                    if (token.StartsWith("-") && token != "-" && token != "--")
                    {
                        return ParseShorts(tokens, options);
                    }
                    if ((token.StartsWith("<") && token.EndsWith(">")) || token.All(c => Char.IsUpper(c)))
                    {
                        result.Add(new Argument(tokens.Move()));
                    }
                    else
                    {
                        result.Add(new Command(tokens.Move()));
                    }
                    break;
            }
            return result;
        }

        private static IEnumerable<Pattern> ParseShorts(Tokens tokens, ICollection<Option> options)
        {
            // shorts ::= '-' ( chars )* [ [ ' ' ] chars ] ;

            var token = tokens.Move();
            Debug.Assert(token.StartsWith("-") && !token.StartsWith("--"));
            var left = token.TrimStart(new[] {'-'});
            var parsed = new List<Pattern>();
            while (left != "")
            {
                var shortName = "-" + left[0];
                left = left.Substring(1);
                var similar = options.Where(o => o.ShortName == shortName).ToList();
                Option option = null;
                if (similar.Count > 1)
                {
                    throw tokens.CreateException(string.Format("{0} is specified ambiguously {1} times", shortName,
                        similar.Count));
                }
                if (similar.Count < 1)
                {
                    option = new Option(shortName, null, 0);
                    options.Add(option);
                    if (tokens.ThrowsInputError)
                    {
                        option = new Option(shortName, null, 0, new ValueObject(true));
                    }
                }
                else
                {
                    // why is copying necessary here?
                    option = new Option(shortName, similar[0].LongName, similar[0].ArgCount, similar[0].Value);
                    ValueObject value = null;
                    if (option.ArgCount != 0)
                    {
                        if (left == "")
                        {
                            if (tokens.Current() == null || tokens.Current() == "--")
                            {
                                throw tokens.CreateException(shortName + " requires argument");
                            }
                            value = new ValueObject(tokens.Move());
                        }
                        else
                        {
                            value = new ValueObject(left);
                            left = "";
                        }
                    }
                    if (tokens.ThrowsInputError)
                        option.Value = value ?? new ValueObject(true);
                }
                parsed.Add(option);
            }
            return parsed;
        }

        private static IEnumerable<Pattern> ParseLong(Tokens tokens, ICollection<Option> options)
        {
            // long ::= '--' chars [ ( ' ' | '=' ) chars ] ;
            var p = new StringPartition(tokens.Move(), "=");
            var longName = p.LeftString;
            Debug.Assert(longName.StartsWith("--"));
            var value = (p.NoSeparatorFound) ? null : new ValueObject(p.RightString);
            var similar = options.Where(o => o.LongName == longName).ToList();
            if (tokens.ThrowsInputError && similar.Count == 0)
            {
                // If not exact match
                similar =
                    options.Where(o => !String.IsNullOrEmpty(o.LongName) && o.LongName.StartsWith(longName)).ToList();
            }
            if (similar.Count > 1)
            {
                // Might be simply specified ambiguously 2+ times?
                throw tokens.CreateException(string.Format("{0} is not a unique prefix: {1}?", longName,
                    string.Join(", ", similar.Select(o => o.LongName))));
            }
            Option option = null;
            if (similar.Count < 1)
            {
                var argCount = p.Separator == "=" ? 1 : 0;
                option = new Option(null, longName, argCount);
                options.Add(option);
                if (tokens.ThrowsInputError)
                    option = new Option(null, longName, argCount, argCount != 0 ? value : new ValueObject(true));
            }
            else
            {
                option = new Option(similar[0].ShortName, similar[0].LongName, similar[0].ArgCount, similar[0].Value);
                if (option.ArgCount == 0)
                {
                    if (value != null)
                        throw tokens.CreateException(option.LongName + " must not have an argument");
                }
                else
                {
                    if (value == null)
                    {
                        if (tokens.Current() == null || tokens.Current() == "--")
                            throw tokens.CreateException(option.LongName + " requires an argument");
                        value = new ValueObject(tokens.Move());
                    }
                }
                if (tokens.ThrowsInputError)
                    option.Value = value ?? new ValueObject(true);
            }
            return new[] {option};
        }

        internal static ICollection<Option> ParseDefaults(string doc)
        {
            var defaults = new List<Option>();
            foreach (var s in ParseSection("options:", doc))
            {
                // FIXME corner case "bla: options: --foo"   

                var p = new StringPartition(s, ":"); // get rid of "options:"
                var optionsText = p.RightString;
                var a = Regex.Split("\n" + optionsText, @"\r?\n[ \t]*(-\S+?)");
                var split = new List<string>();
                for (int i = 1; i < a.Length - 1; i += 2)
                {
                    var s1 = a[i];
                    var s2 = a[i + 1];
                    split.Add(s1 + s2);
                }
                var options = split.Where(x => x.StartsWith("-")).Select(x => Option.Parse(x));
                defaults.AddRange(options);
            }
            return defaults;
        }

        internal static string[] ParseSection(string name, string source)
        {
            var pattern = new Regex(@"^([^\r\n]*" + name + @"[^\r\n]*\r?\n?(?:[ \t].*?(?:\r?\n|$))*)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return (from Match match in pattern.Matches(source) select match.Value.Trim()).ToArray();
        }
    }

    public class PrintExitEventArgs : EventArgs
    {
        public PrintExitEventArgs(string msg, int errorCode)
        {
            Message = msg;
            ErrorCode = errorCode;
        }

        public string Message { get; set; }
        public int ErrorCode { get; set; }
    }

#if NET40
    [Serializable]
#endif
    public class DocoptBaseException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public DocoptBaseException()
        {
        }

        public DocoptBaseException(string message)
            : base(message)
        {
        }

        public DocoptBaseException(string message, Exception inner)
            : base(message, inner)
        {
        }

#if NET40
        protected DocoptBaseException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
#endif

        public int ErrorCode
        {
            get { return 1; }
        }
    }

#if NET40
    [Serializable]
#endif
    public class DocoptExitException : DocoptBaseException
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public DocoptExitException()
        {
        }
        public DocoptExitException(string message) : base(message)
        {
        }
        public DocoptExitException(string message, Exception inner) : base(message, inner)
        {
        }
#if NET40
        protected DocoptExitException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
#endif
    }

#if NET40
    [Serializable]
#endif
    public class DocoptInputErrorException : DocoptBaseException
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public DocoptInputErrorException()
        {
        }
        public DocoptInputErrorException(string message)
            : base(message)
        {
        }
        public DocoptInputErrorException(string message, Exception inner)
            : base(message, inner)
        {
        }
#if NET40
        protected DocoptInputErrorException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

#if NET40
    [Serializable]
#endif
    public class DocoptLanguageErrorException : DocoptBaseException
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public DocoptLanguageErrorException()
        {
        }
        public DocoptLanguageErrorException(string message) : base(message)
        {
        }
        public DocoptLanguageErrorException(string message, Exception inner) : base(message, inner)
        {
        }
#if NET40
        protected DocoptLanguageErrorException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
#endif
    }

    internal class Either : BranchPattern
    {
        public Either(params Pattern[] patterns) : base(patterns)
        {
        }

        public override MatchResult Match(IList<Pattern> left, IEnumerable<Pattern> collected = null)
        {
            var coll = collected ?? new List<Pattern>();
            var outcomes =
                Children.Select(pattern => pattern.Match(left, coll))
                        .Where(outcome => outcome.Matched)
                        .ToList();
            if (outcomes.Count != 0)
            {
                var minCount = outcomes.Min(x => x.Left.Count);
                return outcomes.First(x => x.Left.Count == minCount);
            }
            return new MatchResult(false, left, coll);
        }
    }

    internal static class Extensions
    {
        public static T Bind<T>(this IDictionary<string, ValueObject>args) where T: new()
        {
            return new Binder<T>().Bind(args);
        }
    }
    public static class GenerateCodeHelper
    {
        public static string ConvertDashesToCamelCase(string s)
        {
            // Start with uppercase char
            var makeUpperCase = true;
            var result = "";
            for (int i = 0; i < s.Length; i++)
            {
                if(s[i] == '-')
                {
                    makeUpperCase = true;
                    continue;
                }

                result += makeUpperCase ? char.ToUpperInvariant(s[i]) : s[i];
                makeUpperCase = false;
            }

            return result;
        }
    }

    /// <summary>
    /// Leaf/terminal node of a pattern tree.
    /// </summary>
    internal class LeafPattern: Pattern
    {
        private readonly string _name;

        protected LeafPattern(string name, ValueObject value=null)
        {
            _name = name;
            Value = value;
        }

        protected LeafPattern()
        {
        }

        public override string Name
        {
            get { return _name; }
        }

        public override ICollection<Pattern> Flat(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException("types");
            if (types.Length == 0 || types.Contains(this.GetType()))
            {
                return new Pattern[] { this };
            }
            return new Pattern[] {};
        }

        public virtual SingleMatchResult SingleMatch(IList<Pattern> patterns)
        {
            return new SingleMatchResult();
        }

        public override MatchResult Match(IList<Pattern> left, IEnumerable<Pattern> collected = null)
        {
            var coll = collected ?? new List<Pattern>();
            var sresult = SingleMatch(left);
            var match = sresult.Match;
            if (match == null)
            {
                return new MatchResult(false, left, coll);
            }
            var left_ = new List<Pattern>();
            left_.AddRange(left.Take(sresult.Position));
            left_.AddRange(left.Skip(sresult.Position + 1));
            var sameName = coll.Where(a => a.Name == Name).ToList();
            if (Value != null && (Value.IsList || Value.IsOfTypeInt))
            {
                var increment = new ValueObject(1);
                if (!Value.IsOfTypeInt)
                {
                    increment = match.Value.IsString ? new ValueObject(new [] {match.Value})  : match.Value;
                }
                if (sameName.Count == 0) 
                {
                    match.Value = increment;
                    var res = new List<Pattern>(coll) {match};
                    return new MatchResult(true, left_, res);
                }
                sameName[0].Value.Add(increment);
                return new MatchResult(true, left_, coll);
            }
            var resColl = new List<Pattern>();
            resColl.AddRange(coll);
            resColl.Add(match);
            return new MatchResult(true, left_, resColl);
        }

        public override string ToString()
        {
            return string.Format("{0}({1}, {2})", GetType().Name, Name, Value);
        }
    }

    internal class SingleMatchResult
    {
        public SingleMatchResult(int index, Pattern match)
        {
            Position = index;
            Match = match;
        }

        public SingleMatchResult()
        {
        }

        public int Position { get; set; }
        public Pattern Match { get; set; }
    }

    internal class MatchResult
    {
        public bool Matched;
        public IList<Pattern> Left;
        public IEnumerable<Pattern> Collected;

        public MatchResult() { }

        public MatchResult(bool matched, IList<Pattern> left, IEnumerable<Pattern> collected)
        {
            Matched = matched;
            Left = left;
            Collected = collected;
        }

        public bool LeftIsEmpty { get { return Left.Count == 0; } }

        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ToString().Equals(obj.ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("matched={0} left=[{1}], collected=[{2}]",
                Matched,
                Left == null ? "" : string.Join(", ", Left.Select(p => p.ToString())),
                Collected == null ? "" : string.Join(", ", Collected.Select(p => p.ToString()))
            );
        }
    }

    public enum ValueType { Bool, List, String, }

    public class ArgumentNode : Node
    {
        public ArgumentNode(string name, ValueType valueType) : base(name, valueType) { }
    }

    public class OptionNode : Node
    {
        public OptionNode(string name, ValueType valueType) : base(name, valueType) { }
    }

    public class CommandNode : Node
    {
        public CommandNode(string name) : base(name, ValueType.Bool) { }
    }

    public abstract class Node : IEquatable<Node>
    {
        private class EmptyNode : Node
        {
            public EmptyNode() : base("", (ValueType)0) { }
        }

        /// <summary>
        /// Indicates an empty or non-existant node.
        /// </summary>
        public static readonly Node Empty = new EmptyNode();

        protected Node(string name, ValueType valueType)
        {
            if (name == null) throw new ArgumentNullException("name");

            this.Name = name;
            this.ValueType = valueType;
        }

        public ValueType ValueType { get; private set; }
        public string Name { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", GetType().Name, Name, ValueType);
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode() ^ this.ValueType.GetHashCode();
        }

        public bool Equals(Node other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return other.Name == this.Name
                && other.ValueType == this.ValueType;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(null, obj))
            {
                return false;
            }

            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((Node)obj);
        }
    }


    internal class OneOrMore : BranchPattern
    {
        public OneOrMore(params Pattern[] patterns)
            : base(patterns)
        {
        }

        public override MatchResult Match(IList<Pattern> left, IEnumerable<Pattern> collected = null)
        {
            Debug.Assert(Children.Count == 1);
            var coll = collected ?? new List<Pattern>();
            var l = left;
            var c = coll;
            IList<Pattern> l_ = null;
            var matched = true;
            var times = 0;
            while (matched)
            {
                // could it be that something didn't match but changed l or c?
                var res = Children[0].Match(l, c);
                matched = res.Matched;
                l = res.Left;
                c = res.Collected;
                times += matched ? 1 : 0;
                if (l_ != null && l_.Equals(l))
                    break;
                l_ = l;
            }
            if (times >= 1)
            {
                return new MatchResult(true, l, c);
            }
            return new MatchResult(false, left, coll);
        }
    }

    internal class Option : LeafPattern
    {
        public string ShortName { get; private set; }
        public string LongName { get; private set; }
        public int ArgCount { get; private set; }

        public Option(string shortName = null, string longName = null, int argCount = 0, ValueObject value = null)
            : base()
        {
            ShortName = shortName;
            LongName = longName;
            ArgCount = argCount;
            var v = value ?? new ValueObject(false);
            Value = (v.IsFalse && argCount > 0) ? null : v;
        }

        public Option(string shortName, string longName, int argCount, string value)
            : this(shortName, longName, argCount, new ValueObject(value))
        {
        }

        public override string Name
        {
            get { return LongName ?? ShortName; }
        }

        public override Node ToNode()
        {
            return new OptionNode(this.Name.TrimStart('-'), this.ArgCount == 0 ? ValueType.Bool : ValueType.String);
        }

        public override string GenerateCode()
        {
            var s = Name.ToLowerInvariant();
            s = "Opt" + GenerateCodeHelper.ConvertDashesToCamelCase(s);

            if (ArgCount == 0)
            {
                return string.Format("public bool {0} {{ get {{ return _args[\"{1}\"].IsTrue; }} }}", s, Name);
            }
            var defaultValue = Value == null ? "null" : string.Format("\"{0}\"", Value);
            return string.Format("public string {0} {{ get {{ return null == _args[\"{1}\"] ? {2} : _args[\"{1}\"].ToString(); }} }}", s, Name, defaultValue);
        }

        public override SingleMatchResult SingleMatch(IList<Pattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].Name == Name)
                    return new SingleMatchResult(i, left[i]);
            }
            return new SingleMatchResult();
        }

        public override string ToString()
        {
            return string.Format("Option({0},{1},{2},{3})", ShortName, LongName, ArgCount, Value);
        }

        private const string DESC_SEPARATOR = "  ";

        public static Option Parse(string optionDescription)
        {
            if (optionDescription == null) throw new ArgumentNullException("optionDescription");

            string shortName = null;
            string longName = null;
            var argCount = 0;
            var value = new ValueObject(false);
            var p = new StringPartition(optionDescription, DESC_SEPARATOR);
            var options = p.LeftString;
            var description = p.RightString;
            foreach (var s in options.Split(" \t,=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                if (s.StartsWith("--"))
                    longName = s;
                else if (s.StartsWith("-"))
                {
                    shortName = s;
                }
                else
                {
                    argCount = 1;
                }
            }
            if (argCount > 0)
            {
                var r = new Regex(@"\[default: (.*)\]", RegexOptions.IgnoreCase);
                var m = r.Match(description);
                value = m.Success ? new ValueObject(m.Groups[1].Value) : null;
            }
            return new Option(shortName, longName, argCount, value);
        }
    }

    internal class Optional : BranchPattern
    {
        public Optional(params Pattern[] patterns) : base(patterns)
        {
            
        }

        public override MatchResult Match(IList<Pattern> left, IEnumerable<Pattern> collected = null)
        {
            var c = collected ?? new List<Pattern>();
            var l = left;
            foreach (var pattern in Children)
            {
                var res = pattern.Match(l, c);
                l = res.Left;
                c = res.Collected;
            }
            return new MatchResult(true, l, c);
        }
    }
    /// <summary>
    ///     Marker/placeholder for [options] shortcut.
    /// </summary>
    internal class OptionsShortcut : Optional
    {
        public OptionsShortcut() : base(new Pattern[0])
        {
        }
    }

    internal abstract class Pattern
    {
        public ValueObject Value { get; set; }

        public virtual string Name
        {
            get { return ToString(); }
        }

        public virtual string GenerateCode()
        {
            return "// No code for " + Name;
        }

        public virtual Node ToNode()
        {
            return null;
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ToString() == obj.ToString();
        }

// override object.GetHashCode
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public virtual bool HasChildren
        {
            get { return false; }
        }

        public IList<Pattern> Children { get; set; }

        public Pattern Fix()
        {
            FixIdentities();
            FixRepeatingArguments();
            return this;
        }

        /// <summary>
        ///     Make pattern-tree tips point to same object if they are equal.
        /// </summary>
        /// <param name="uniq"></param>
        public void FixIdentities(ICollection<Pattern> uniq = null)
        {
            var listUniq = uniq ?? Flat().Distinct().ToList();
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (!child.HasChildren)
                {
                    Debug.Assert(listUniq.Contains(child));
                    Children[i] = listUniq.First(p => p.Equals(child));
                }
                else
                {
                    child.FixIdentities(listUniq);
                }
            }
        }


        public Pattern FixRepeatingArguments()
        {
            var transform = Transform(this);
            var either = transform.Children.Select(c => c.Children);
            foreach (var aCase in either)
            {
                var cx = aCase.ToList();
                var l = aCase.Where(e => cx.Count(c2 => c2.Equals(e)) > 1).ToList();

                foreach (var e in l)
                {
                    if (e is Argument || (e is Option && (e as Option).ArgCount > 0))
                    {
                        if (e.Value == null)
                        {
                            e.Value = new ValueObject(new ArrayList());
                        }
                        else if (!e.Value.IsList)
                        {
                            e.Value =
                                new ValueObject(e.Value.ToString()
                                                 .Split(new char[0], StringSplitOptions.RemoveEmptyEntries));
                        }
                    }
                    if (e is Command || (e is Option && (e as Option).ArgCount == 0))
                    {
                        e.Value = new ValueObject(0);
                    }
                }
            }
            return this;
        }

        /// <summary>
        ///     Expand pattern into an (almost) equivalent one, but with single Either.
        ///     Example: ((-a | -b) (-c | -d)) => (-a -c | -a -d | -b -c | -b -d)
        ///     Quirks: [-a] => (-a), (-a...) => (-a -a)
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static Either Transform(Pattern pattern)
        {
            var result = new List<IList<Pattern>>();
            var groups = new List<IList<Pattern>> {new List<Pattern> {pattern}};
            while (groups.Count > 0)
            {
                var children = groups[0];
                groups.RemoveAt(0);
                var parents = new[]
                    {
                        typeof (Required), typeof (Optional), typeof (OptionsShortcut), typeof (Either), typeof (OneOrMore)
                    };
                if (parents.Any(t => children.Any(c => c.GetType() == t)))
                {
                    var child = children.First(c => parents.Contains(c.GetType()));
                    children.Remove(child);
                    if (child is Either)
                    {
                        foreach (var c in (child as Either).Children)
                        {
                            var l = new List<Pattern> {c};
                            l.AddRange(children);
                            groups.Add(l);
                        }
                    }
                    else if (child is OneOrMore)
                    {
                        var l = new List<Pattern>();
                        l.AddRange((child as OneOrMore).Children);
                        l.AddRange((child as OneOrMore).Children); // add twice
                        l.AddRange(children);
                        groups.Add(l);
                    }
                    else
                    {
                        var l = new List<Pattern>();
                        if (child.HasChildren)
                            l.AddRange(child.Children);
                        l.AddRange(children);
                        groups.Add(l);
                    }
                }
                else
                {
                    result.Add(children);
                }
            }
            return new Either(result.Select(r => new Required(r.ToArray()) as Pattern).ToArray());
        }

        public virtual MatchResult Match(IList<Pattern> left, IEnumerable<Pattern> collected = null)
        {
            return new MatchResult();
        }

        public abstract ICollection<Pattern> Flat(params Type[] types);

        /// <summary>
        ///     Flattens the current patterns to the leaves only
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Pattern> Flat()
        {
            return Flat(new Type[0]);
        }
    }

    internal class Required : BranchPattern
    {
        public Required(params Pattern[] patterns)
            : base(patterns)
        {
        }

        public override MatchResult Match(IList<Pattern> left,
                                          IEnumerable<Pattern> collected = null)
        {
            var coll = collected ?? new List<Pattern>();
            var l = left;
            var c = coll;
            foreach (var pattern in Children)
            {
                var res = pattern.Match(l, c);
                l = res.Left;
                c = res.Collected;
                if (!res.Matched)
                    return new MatchResult(false, left, coll);
            }
            return new MatchResult(true, l, c);
        }
    }
    public struct StringPartition
    {
        public string LeftString;
        public string Separator;
        public string RightString;

        /// <summary>
        /// Split the <paramref name="stringToPartition"/> at the first occurrence of <paramref name="separator"/>, and stores the part before the separator,
        /// the separator itself, and the part after the separator. If the separator is not found, stores the string itself, and
        /// two empty strings.
        /// </summary>
        /// <param name="stringToPartition"></param>
        /// <param name="separator"></param>
        public StringPartition(string stringToPartition, string separator)
        {
            LeftString = stringToPartition;
            Separator = "";
            RightString = "";

            var i = stringToPartition.IndexOf(separator, System.StringComparison.Ordinal);
            if (i > 0)
            {
                LeftString = stringToPartition.Substring(0, i);
                Separator = separator;
                RightString = stringToPartition.Substring(i + separator.Length);
            }
        }

        public bool NoSeparatorFound 
        {
            get { return Separator=="" && RightString == ""; }
        }
    }

    public class Tokens: IEnumerable<string>
    {
        private readonly Type _errorType;
        private readonly List<string> _tokens = new List<string>();

        public Tokens(IEnumerable<string> source, Type errorType)
        {
            _errorType = errorType ?? typeof(DocoptInputErrorException);
            _tokens.AddRange(source);
        }

        public Tokens(string source, Type errorType)
        {
            _errorType = errorType ?? typeof(DocoptInputErrorException);
            _tokens.AddRange(source.Split(new char[0], StringSplitOptions.RemoveEmptyEntries));
        }

        public Type ErrorType
        {
            get { return _errorType; }
        }

        public bool ThrowsInputError
        {
            get { return ErrorType == typeof (DocoptInputErrorException); }
        }


        public static Tokens FromPattern(string pattern)
        {
            var spacedOut = Regex.Replace(pattern, @"([\[\]\(\)\|]|\.\.\.)", @" $1 ");
            var source = Regex.Split(spacedOut, @"\s+|(\S*<.*?>)").Where(x => !string.IsNullOrEmpty(x));
            return new Tokens(source, typeof(DocoptLanguageErrorException));
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _tokens.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public string Move()
        {
            string s = null;
            if (_tokens.Count > 0)
            {
                s = _tokens[0];
                _tokens.RemoveAt(0);
            }
            return s;
        }

        public string Current()
        {
            return (_tokens.Count > 0) ? _tokens[0] : null;
        }

        public Exception CreateException(string message)
        {
            return Activator.CreateInstance(_errorType, new object[] {message}) as Exception;
        }

        public override string ToString()
        {
            return string.Format("current={0},count={1}", Current(), _tokens.Count);
        }
    }

    public class ValueObject
    {
        public object Value { get; private set; }

        internal ValueObject(object obj)
        {
            if (obj is ArrayList)
            {
                Value = new ArrayList(obj as ArrayList);
                return;
            }
            if (obj is ICollection)
            {
                Value = new ArrayList(obj as ICollection);
                return;
            }
            Value = obj;
        }

        internal ValueObject()
        {
            Value = null;
        }

        public bool IsNullOrEmpty
        {
            get { return Value == null || Value.ToString() == ""; }
        }

        public bool IsFalse
        {
            get { return (Value as bool?) == false; }
        }

        public bool IsTrue
        {
            get { return (Value as bool?) == true; }
        }

        public bool IsList
        {
            get { return Value is ArrayList; }
        }

        internal bool IsOfTypeInt
        {
            get { return Value is int?; }
        }

        public bool IsInt
        {
            get
            {
                int value;
                return Value != null && (Value is int || Int32.TryParse(Value.ToString(), out value));
            }
        }

        public int AsInt
        {
            get { return IsList ? 0 : Convert.ToInt32(Value); }
        }

        public bool IsString
        {
            get { return Value is string; }
        }

        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var v = (obj as ValueObject).Value;
            if (Value == null && v == null) return true;
            if (Value == null || v == null) return false;
            if (IsList || (obj as ValueObject).IsList)
                return Value.ToString().Equals(v.ToString());
            return Value.Equals(v);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            if (IsList)
            {
                var l = (from object v in AsList select v.ToString()).ToList();
                return string.Format("[{0}]", String.Join(", ", l));
            }
            return (Value ?? "").ToString();
        }

        internal void Add(ValueObject increment)
        {
            if (increment == null) throw new ArgumentNullException("increment");

            if (increment.Value == null) throw new InvalidOperationException("increment.Value is null");

            if (Value == null) throw new InvalidOperationException("Value is null");

            if (increment.IsOfTypeInt)
            {
                if (IsList)
                    (Value as ArrayList).Add(increment.AsInt);
                else
                    Value = increment.AsInt + AsInt;
            }
            else
            {
                var l = new ArrayList();
                if (IsList)
                {
                    l.AddRange(AsList);
                }
                else
                {
                    l.Add(Value);
                }
                if (increment.IsList)
                {
                    l.AddRange(increment.AsList);
                }
                else
                {
                    l.Add(increment);
                }
                Value = l;
            }
        }

        public ArrayList AsList
        {
            get { return IsList ? (Value as ArrayList) : (new ArrayList(new[] {Value})); }
        }
    }
}
