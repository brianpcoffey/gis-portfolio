// csmetrics — syntax-only C# complexity measurement (cyclomatic + cognitive) per method-like member.
//
// Usage (from the repo root; both arguments are optional):
//   dotnet run -c Release --project tools/metrics/csmetrics -- [repoRoot=.] [out.csv=cs-metrics.csv]
//   py -I tools/metrics/summarize_cs.py cs-metrics.csv [topN]
// This project is NOT in Portfolio.sln and opts out of central package management, so
// `dotnet build Portfolio.sln` never builds it.
//
// Walks every *.cs under repoRoot (skipping bin/ obj/ .git/ build/ .vs/ TestResults*), parses each file with
// Roslyn (LanguageVersion.Preview, no semantic model) and emits one CSV row per method-like member:
//   category,project,file,line,endLine,loc,kind,type,member,cyclomatic,cognitive
// category: prod | tests | benchmarks | migrations | generated | tools
//
// Member granularity (matches CA1502 / Sonar S3776 conventions):
//   * methods, constructors, destructors, operators, conversion operators
//   * property/indexer/event accessors with a body (get/set/init/add/remove), expression-bodied properties
//   * lambdas, anonymous methods and local functions are folded INTO their enclosing member
//   * top-level statements (Program.cs) are one member "<top-level statements>"; local functions declared
//     directly at top level are reported as their own members (they are de-facto static helpers)
//   * field / property initializers are reported only when they contain branching
//
// Cyclomatic (McCabe, 1 + decision points), counted on syntax:
//   if, ?:, while, do, for, foreach, case label (each, not default), switch-expression arm (not a trailing
//   bare `_`), catch, &&, ||, ??, ??=, ?. , pattern `and`/`or`
//   (CA1502 uses the IOperation tree; see the calibration notes in findings-complexity.md for differences.)
//
// Cognitive (SonarSource spec, as implemented by sonar-dotnet):
//   +1+nesting : if (not else-if), ?:, switch statement, switch expression, for, foreach, while, do, catch
//   +1         : else if, else, goto, each new sequence of like logical operators (&&, ||, pattern and/or),
//                recursion (heuristic: invocation of the enclosing method's simple name with the same arity)
//   nesting+1  : bodies of the above, lambdas, anonymous methods, local functions
//   no cost    : ??, ?. , try, finally, using, lock, break, continue, return, throw
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length > 2 || args.Any(a => a is "-h" or "--help"))
{
    Console.Error.WriteLine("usage: csmetrics [repoRoot=.] [out.csv=cs-metrics.csv]");
    return 2;
}

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var outPath = Path.GetFullPath(args.Length > 1 ? args[1] : "cs-metrics.csv");
var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", "build", ".vs", "TestResults", "TestResults-native", "node_modules" };
var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
var rows = new List<string>();
int files = 0;

foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
    var segs = rel.Split('/');
    if (segs.Take(segs.Length - 1).Any(skipDirs.Contains)) continue;
    files++;
    var text = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: rel);
    var unit = (CompilationUnitSyntax)tree.GetRoot();
    var category = Categorize(rel);
    var project = segs.Length > 1 ? segs[0] : "(root)";

    // Top-level statements
    var globals = unit.Members.OfType<GlobalStatementSyntax>().ToList();
    var topLevel = globals.Where(g => g.Statement is not LocalFunctionStatementSyntax).ToList();
    if (topLevel.Count > 0)
    {
        var cyc = 1 + topLevel.Sum(g => CyclomaticPoints(g.Statement));
        // CA1502 measures the synthesized <Main>$, whose operation tree also contains the top-level local functions.
        var cycCa = 1 + globals.Sum(g => CyclomaticPointsCa1502(g.Statement));
        var walker = new CognitiveWalker(methodName: null, arity: -1);
        foreach (var g in topLevel) walker.Visit(g.Statement);
        var (l1, l2) = Lines(topLevel.First(), topLevel.Last());
        rows.Add(Row(category, project, rel, l1, l2, "toplevel", "Program", "<top-level statements>", cyc, walker.Complexity, cycCa));
    }
    foreach (var g in globals.Where(g => g.Statement is LocalFunctionStatementSyntax))
    {
        var lf = (LocalFunctionStatementSyntax)g.Statement;
        AddMember(lf, "localfunc", "Program", lf.Identifier.Text, lf.ParameterList.Parameters.Count, (SyntaxNode?)lf.Body ?? lf.ExpressionBody);
    }

    foreach (var node in unit.DescendantNodes())
    {
        switch (node)
        {
            case MethodDeclarationSyntax m:
                AddMember(m, "method", TypeName(m), m.Identifier.Text, m.ParameterList.Parameters.Count, (SyntaxNode?)m.Body ?? m.ExpressionBody);
                break;
            case ConstructorDeclarationSyntax c:
                AddMember(c, "ctor", TypeName(c), ".ctor", c.ParameterList.Parameters.Count, (SyntaxNode?)c.Body ?? c.ExpressionBody, c.Initializer);
                break;
            case DestructorDeclarationSyntax d:
                AddMember(d, "dtor", TypeName(d), "~" + d.Identifier.Text, 0, (SyntaxNode?)d.Body ?? d.ExpressionBody);
                break;
            case OperatorDeclarationSyntax o:
                AddMember(o, "operator", TypeName(o), "op_" + o.OperatorToken.Text, o.ParameterList.Parameters.Count, (SyntaxNode?)o.Body ?? o.ExpressionBody);
                break;
            case ConversionOperatorDeclarationSyntax co:
                AddMember(co, "conversion", TypeName(co), "op_" + co.ImplicitOrExplicitKeyword.Text + "_" + co.Type, co.ParameterList.Parameters.Count, (SyntaxNode?)co.Body ?? co.ExpressionBody);
                break;
            case AccessorDeclarationSyntax a when a.Body is not null || a.ExpressionBody is not null:
            {
                var owner = a.Parent?.Parent switch
                {
                    PropertyDeclarationSyntax p => p.Identifier.Text,
                    IndexerDeclarationSyntax => "this[]",
                    EventDeclarationSyntax e => e.Identifier.Text,
                    _ => "?"
                };
                AddMember(a, "accessor", TypeName(a), owner + "." + a.Keyword.Text, -1, (SyntaxNode?)a.Body ?? a.ExpressionBody);
                break;
            }
            case PropertyDeclarationSyntax p when p.ExpressionBody is not null:
                AddMember(p, "accessor", TypeName(p), p.Identifier.Text + ".get", -1, p.ExpressionBody);
                break;
            case PropertyDeclarationSyntax p when p.Initializer is not null && CyclomaticPoints(p.Initializer) > 0:
                AddMember(p, "initializer", TypeName(p), p.Identifier.Text + " (initializer)", -1, p.Initializer);
                break;
            case IndexerDeclarationSyntax ix when ix.ExpressionBody is not null:
                AddMember(ix, "accessor", TypeName(ix), "this[].get", -1, ix.ExpressionBody);
                break;
            case FieldDeclarationSyntax f:
                foreach (var v in f.Declaration.Variables)
                    if (v.Initializer is not null && CyclomaticPoints(v.Initializer) > 0)
                        AddMember(v, "initializer", TypeName(f), v.Identifier.Text + " (initializer)", -1, v.Initializer);
                break;
        }
    }

    void AddMember(SyntaxNode decl, string kind, string type, string name, int arity, SyntaxNode? body, SyntaxNode? extra = null)
    {
        var (l1, l2) = Lines(decl, decl);
        var lineOfName = decl switch
        {
            MethodDeclarationSyntax m => m.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            ConstructorDeclarationSyntax c => c.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            LocalFunctionStatementSyntax lf => lf.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            _ => l1
        };
        if (body is null && extra is null)
        {
            rows.Add(Row(category, project, rel, lineOfName, l2, kind, type, name, 1, 0, 1));
            return;
        }
        int cyc = 1, cycCa = 1;
        var walker = new CognitiveWalker(kind is "method" or "localfunc" ? name : null, arity);
        if (extra is not null) { cyc += CyclomaticPoints(extra); cycCa += CyclomaticPointsCa1502(extra); walker.Visit(extra); }
        if (body is not null) { cyc += CyclomaticPoints(body); cycCa += CyclomaticPointsCa1502(body); walker.Visit(body); }
        rows.Add(Row(category, project, rel, lineOfName, l2, kind, type, name, cyc, walker.Complexity, cycCa));
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var sb = new StringBuilder();
sb.AppendLine("category,project,file,line,endLine,loc,kind,type,member,cyclomatic,cognitive,cyclomatic_ca1502");
foreach (var r in rows) sb.AppendLine(r);
File.WriteAllText(outPath, sb.ToString());
Console.WriteLine($"files={files} members={rows.Count} -> {outPath}");
return 0;

static string Categorize(string rel)
{
    if (rel.Contains("/Migrations/", StringComparison.Ordinal)) return "migrations";
    if (rel.EndsWith("Portfolio.Services/Data/RedlandsRoadNetwork.cs", StringComparison.Ordinal)) return "generated";
    if (rel.StartsWith("Portfolio.Tests/", StringComparison.Ordinal)) return "tests";
    if (rel.StartsWith("Portfolio.Benchmarks/", StringComparison.Ordinal)) return "benchmarks";
    if (rel.StartsWith("tools/", StringComparison.Ordinal)) return "tools";
    return "prod";
}

static (int, int) Lines(SyntaxNode first, SyntaxNode last)
{
    var a = first.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    var b = last.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
    return (a, b);
}

static string TypeName(SyntaxNode node)
{
    var parts = new List<string>();
    for (var p = node.Parent; p is not null; p = p.Parent)
        if (p is BaseTypeDeclarationSyntax t) parts.Add(t.Identifier.Text);
    parts.Reverse();
    return parts.Count == 0 ? "(none)" : string.Join('.', parts);
}

static string Row(string category, string project, string file, int line, int endLine, string kind, string type, string member, int cyc, int cog, int cycCa)
{
    static string Q(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    return string.Join(',', Q(category), Q(project), Q(file), line.ToString(CultureInfo.InvariantCulture),
        endLine.ToString(CultureInfo.InvariantCulture), (endLine - line + 1).ToString(CultureInfo.InvariantCulture),
        Q(kind), Q(type), Q(member), cyc.ToString(CultureInfo.InvariantCulture), cog.ToString(CultureInfo.InvariantCulture),
        cycCa.ToString(CultureInfo.InvariantCulture));
}

// CA1502-compatible variant (calibrated against the real analyzer on this repo): the IOperation-based metric
// does NOT count catch clauses, `and`/`or` patterns, switch-expression arms, or `??=`.
static int CyclomaticPointsCa1502(SyntaxNode node)
{
    int c = 0;
    foreach (var n in node.DescendantNodesAndSelf())
    {
        switch (n)
        {
            case IfStatementSyntax:
            case ConditionalExpressionSyntax:
            case WhileStatementSyntax:
            case DoStatementSyntax:
            case ForStatementSyntax:
            case CommonForEachStatementSyntax:
            case CaseSwitchLabelSyntax:
            case CasePatternSwitchLabelSyntax:
            case ConditionalAccessExpressionSyntax:
                c++;
                break;
            case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression) || b.IsKind(SyntaxKind.CoalesceExpression):
                c++;
                break;
        }
    }
    return c;
}

static int CyclomaticPoints(SyntaxNode node)
{
    int c = 0;
    foreach (var n in node.DescendantNodesAndSelf())
    {
        switch (n)
        {
            case IfStatementSyntax:
            case ConditionalExpressionSyntax:
            case WhileStatementSyntax:
            case DoStatementSyntax:
            case ForStatementSyntax:
            case CommonForEachStatementSyntax:
            case CaseSwitchLabelSyntax:
            case CasePatternSwitchLabelSyntax:
            case CatchClauseSyntax:
            case ConditionalAccessExpressionSyntax:
            case BinaryPatternSyntax:
                c++;
                break;
            case SwitchExpressionArmSyntax arm:
                if (!(arm.Pattern is DiscardPatternSyntax && arm.WhenClause is null)) c++;
                break;
            case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression) || b.IsKind(SyntaxKind.CoalesceExpression):
                c++;
                break;
            case AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.CoalesceAssignmentExpression):
                c++;
                break;
        }
    }
    return c;
}

sealed class CognitiveWalker : CSharpSyntaxWalker
{
    private readonly string? _methodName;
    private readonly int _arity;
    private int _nesting;
    private readonly HashSet<SyntaxNode> _logicalToIgnore = new();
    public int Complexity { get; private set; }

    public CognitiveWalker(string? methodName, int arity) { _methodName = methodName; _arity = arity; }

    private void Nested(SyntaxNode? node)
    {
        if (node is null) return;
        _nesting++;
        Visit(node);
        _nesting--;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        if (node.Parent is ElseClauseSyntax) Complexity += 1;       // else-if: +1, no nesting penalty
        else Complexity += 1 + _nesting;
        Visit(node.Condition);
        Nested(node.Statement);
        if (node.Else is not null)
        {
            if (node.Else.Statement is IfStatementSyntax elseIf)
            {
                VisitIfStatement(elseIf);                            // same chain level
            }
            else
            {
                Complexity += 1;                                     // plain else
                Nested(node.Else.Statement);
            }
        }
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Complexity += 1 + _nesting;
        Nested2(node);
    }

    private void Nested2(SyntaxNode node)
    {
        _nesting++;
        foreach (var child in node.ChildNodes()) Visit(child);
        _nesting--;
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitSwitchExpression(SwitchExpressionSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitForStatement(ForStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitForEachStatement(ForEachStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitWhileStatement(WhileStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitDoStatement(DoStatementSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitCatchClause(CatchClauseSyntax node) { Complexity += 1 + _nesting; Nested2(node); }
    public override void VisitGotoStatement(GotoStatementSyntax node) { Complexity += 1; base.VisitGotoStatement(node); }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => Nested2(node);
    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => Nested2(node);
    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => Nested2(node);
    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => Nested2(node);

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var kind = node.Kind();
        if ((kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.LogicalOrExpression) && !_logicalToIgnore.Contains(node))
        {
            var left = Unparen(node.Left);
            if (!left.IsKind(kind)) Complexity += 1;
            var right = Unparen(node.Right);
            if (right.IsKind(kind)) _logicalToIgnore.Add(right);
        }
        base.VisitBinaryExpression(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        var kind = node.Kind();
        if (!_logicalToIgnore.Contains(node))
        {
            var left = UnparenPattern(node.Left);
            if (!left.IsKind(kind)) Complexity += 1;
            var right = UnparenPattern(node.Right);
            if (right.IsKind(kind)) _logicalToIgnore.Add(right);
        }
        base.VisitBinaryPattern(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_methodName is not null)
        {
            var name = node.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                GenericNameSyntax g => g.Identifier.Text,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } ma => ma.Name.Identifier.Text,
                _ => null
            };
            if (name == _methodName && (_arity < 0 || node.ArgumentList.Arguments.Count == _arity)) Complexity += 1;
        }
        base.VisitInvocationExpression(node);
    }

    private static SyntaxNode Unparen(ExpressionSyntax e)
    {
        while (e is ParenthesizedExpressionSyntax p) e = p.Expression;
        return e;
    }

    private static SyntaxNode UnparenPattern(PatternSyntax p)
    {
        while (p is ParenthesizedPatternSyntax pp) p = pp.Pattern;
        return p;
    }
}
