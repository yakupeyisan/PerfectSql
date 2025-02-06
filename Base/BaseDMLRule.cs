using System.Linq.Expressions;

namespace PerfectSql.Base;

public abstract class BaseDMLRule<T>
    where T : class, new()
{
    private List<RuleModel<T>> Predicates = new();
    protected void AddRule(Expression<Func<T, bool>> rule)
    {
        Predicates.Add(new(rule));
    }
    protected void AddRule(Expression<Func<T, bool>> rule, string message)
    {
        Predicates.Add(new(rule, message));
    }
    internal void Run(T model)
    {
        var exception = new PerfectSqlExceptionModel(typeof(T).FullName + " exceptions.");
        bool hasError = false;
        foreach (var item in Predicates)
        {
            var func = item.Rule.Compile();
            if (func(model))
            {
                exception.Errors.Add(item.Message);
                hasError = true;
            }
        }
        if (hasError) throw exception;
    }
}
internal class RuleModel<T>
    where T : class, new()
{
    public Expression<Func<T, bool>> Rule { get; private set; }
    public string Message { get; private set; }
    public RuleModel(Expression<Func<T, bool>> rule)
    {
        Rule = rule;
        Message = GetPropertyName(rule) + "_has_error";
    }
    public RuleModel(Expression<Func<T, bool>> rule, string message) : this(rule)
    {
        Message = message;
    }
    private static string GetPropertyName(Expression<Func<T, bool>> expression)
    {
        return GetPropertyName(expression.Body);
    }
    private static string GetPropertyName(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }
        if (expression is UnaryExpression unaryExpression
            && unaryExpression.Operand is MemberExpression memberExp)
        {
            return memberExp.Member.Name;
        }
        if (expression is BinaryExpression binaryExpression)
        {
            return GetPropertyName(binaryExpression.Left);
        }
        if (expression is MethodCallExpression methodCallExpression
            && methodCallExpression.Arguments.FirstOrDefault() is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }
        throw new ArgumentException("Geçersiz ifade!");
    }
}
[Serializable]
public class PerfectSqlExceptionModel : Exception
{
    public List<string> Errors { get; internal set; } = new();
    public PerfectSqlExceptionModel(string message) : base(message)
    {

    }
}