using PerfectSql.Base;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace PerfectSql;

public class QueryBuilder<TEntity>
    where TEntity : Entity, new()
{
    private static QueryBuilder<TEntity> _instance;
    public static QueryBuilder<TEntity> Instance => _instance ??= new QueryBuilder<TEntity>();
    private interface IItem
    {
        object GetValue();
        IEnumerable<object> GetValues();
    }
    private class SingleItem : IItem
    {
        public object Value { get; set; }

        public SingleItem(object value)
        {
            Value = value;
        }
        public override string ToString()
        {
            return Value.ToString();
        }

        public object GetValue() => Value;

        public IEnumerable<object> GetValues() => null;
    }
    private class MultipleItem<T> : IItem
    {
        public IEnumerable<T> Values { get; set; }
        public MultipleItem(IEnumerable<T> values)
        {
            Values = values;
        }
        public override string ToString()
        {
            return string.Join(", ", Values);
        }
        public object GetValue() => null;

        public IEnumerable<object> GetValues() => Values.Select(x => (object)x);
    }
    private string ConditionQuery = "";
    private IDictionary<string, string> JoinQuery = new Dictionary<string, string>();
    private IDictionary<string, string> ListJoinQuery = new Dictionary<string, string>();
    private IDictionary<string, PropertyInfo[]> JoinTypes = new Dictionary<string, PropertyInfo[]>();
    private string SelectQuery = "*";
    private string TableName;
    private bool IsSelectAll = true;
    private IDictionary<string, IItem> Values = new Dictionary<string, IItem>();
    private string OrderString = "";
    private long OffsetValue = 0;
    private long LimitValue = -1;
    public QueryBuilder()
    {
        if (!ConnectionStore.IsLoaded)
        {
            throw new PerfectSqlException("Connection not found connection string. Before load connection");
        }
        TableName = ToPlural(typeof(TEntity).Name);
        SelectAll();
    }
    #region Helpers
    private void Clear()
    {
        JoinQuery = new Dictionary<string, string>();
        ListJoinQuery = new Dictionary<string, string>();
        Values = new Dictionary<string, IItem>();
        JoinTypes = new Dictionary<string, PropertyInfo[]>();
        SelectQuery = "*";
        OrderString = "";
        OffsetValue = 0;
        LimitValue = -1;
    }
    private string ToPlural(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word; // Eğer boş veya null bir kelime verilirse, aynı kelimeyi geri döndürür.
        }

        // Küçük harfler ile kontrol et
        string lowerWord = word.ToLower();

        // Özel çoğul kurallarını ele al
        var specialPlurals = new Dictionary<string, string>
    {
        { "child", "children" },
        { "man", "men" },
        { "woman", "women" },
        { "foot", "feet" },
        { "tooth", "teeth" },
        { "mouse", "mice" },
        { "person", "people" },
        { "baby", "babies" },
        { "box", "boxes" },
        { "bus", "buses" },
        { "fox", "foxes" },
        { "hero", "heroes" },
        { "leaf", "leaves" },
        { "wife", "wives" },
        { "knife", "knives" },
        { "cactus", "cacti" },
        { "focus", "foci" },
        { "radius", "radii" },
        { "analysis", "analyses" },
        { "criterion", "criteria" },
        { "appendix", "appendices" },
        { "oasis", "oases" }
    };

        // Eğer kelime özel bir çoğul kurala sahipse, onu döndür
        if (specialPlurals.ContainsKey(lowerWord))
        {
            return specialPlurals[lowerWord];
        }

        // Eğer kelime "y" ile bitiyorsa, sonunu "ies" yap
        if (lowerWord.EndsWith("y"))
        {
            return word.Substring(0, word.Length - 1) + "ies";
        }

        // Eğer kelime "o", "s", "x", "z", "ch", "sh" ile bitiyorsa "es" ekle
        if (lowerWord.EndsWith("o") || lowerWord.EndsWith("s") || lowerWord.EndsWith("x") || lowerWord.EndsWith("z") ||
            lowerWord.EndsWith("ch") || lowerWord.EndsWith("sh"))
        {
            return word + "es";
        }

        // Diğer tüm durumlar için, "s" ekleyerek çoğul yap
        return word + "s";
    }
    private string ToSingular(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word; // Eğer boş veya null bir kelime verilirse, aynı kelimeyi geri döndürür.
        }

        // Küçük harfler ile kontrol et
        string lowerWord = word.ToLower();

        // Özel tekil kuralları (çoğul -> tekil)
        var specialSingulars = new Dictionary<string, string>
    {
        { "children", "child" },
        { "men", "man" },
        { "women", "woman" },
        { "feet", "foot" },
        { "teeth", "tooth" },
        { "mice", "mouse" },
        { "people", "person" },
        { "babies", "baby" },
        { "boxes", "box" },
        { "buses", "bus" },
        { "foxes", "fox" },
        { "heroes", "hero" },
        { "leaves", "leaf" },
        { "wives", "wife" },
        { "knives", "knife" },
        { "cacti", "cactus" },
        { "foci", "focus" },
        { "radii", "radius" },
        { "analyses", "analysis" },
        { "criteria", "criterion" },
        { "appendices", "appendix" },
        { "oases", "oasis" }
    };

        // Eğer kelime özel bir tekil kurala sahipse, onu döndür
        if (specialSingulars.ContainsKey(lowerWord))
        {
            return specialSingulars[lowerWord];
        }

        // Eğer kelime "ies" ile bitiyorsa ve öncesinde sesli harf değilse, sonunu "y" yap
        if (lowerWord.EndsWith("ies") && word.Length > 3 && !"aeiou".Contains(word[word.Length - 4]))
        {
            return word.Substring(0, word.Length - 3) + "y";
        }

        // Eğer kelime "es" ile bitiyorsa ve "o", "s", "x", "z", "ch", "sh" öncesindeyse, "es" çıkar
        if (lowerWord.EndsWith("es") &&
            (lowerWord.EndsWith("oes") || lowerWord.EndsWith("ses") || lowerWord.EndsWith("xes") || lowerWord.EndsWith("zes") ||
             lowerWord.EndsWith("ches") || lowerWord.EndsWith("shes")))
        {
            return word.Substring(0, word.Length - 2);
        }

        // Eğer kelime "s" ile bitiyorsa, "s" çıkar
        if (lowerWord.EndsWith("s") && word.Length > 1)
        {
            return word.Substring(0, word.Length - 1);
        }

        // Diğer tüm durumlar için, aynı kelimeyi döndür
        return word;
    }

    private string GenerateRandomString(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var random = new Random();
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
    private string GetCollectionName(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }
        throw new InvalidOperationException("Expression must point to a collection.");
    }
    public Type GetMemberType(MemberExpression memberExpression)
    {
        if (memberExpression.Member is PropertyInfo property)
        {
            return property.PropertyType;
        }
        else if (memberExpression.Member is FieldInfo field)
        {
            return field.FieldType;
        }
        throw new InvalidOperationException("Unsupported member type.");
    }
    private string GetPropertyPath(Expression expression)
    {
        if (expression == null)
            return string.Empty;
        if (expression is UnaryExpression unaryExpression)
            return GetPropertyPath(unaryExpression.Operand);

        if (expression is MemberExpression memberExpression)
        {
            string parentPath = GetPropertyPath(memberExpression.Expression);
            return string.IsNullOrEmpty(parentPath)
                ? memberExpression.Member.Name
                : $"{parentPath}.{memberExpression.Member.Name}";
        }
        return string.Empty;
    }
    #endregion
    #region Generic
    private string PureWhere<TProperty>(Expression<Func<TEntity, TProperty>> prop, string logic, string condition, int count = 1)
    {
        string propertyPath = GetPropertyPath(prop.Body);
        if (!propertyPath.Contains("."))
        {
            propertyPath = typeof(TEntity).FullName.Split(".").Last() + "." + propertyPath;
        }
        var key = GenerateRandomString();
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += $" {logic} ";
        }
        if (condition == "IN" || condition == "NOT IN")
        {
            ConditionQuery += $" {propertyPath} {condition} (";
            for (int i = 0; i < count; i++)
            {
                ConditionQuery += $"@{key}_{i}";
                if (i < count - 1)
                {
                    ConditionQuery += $",";
                }
            }
            ConditionQuery += $")";
        }
        else
        {
            ConditionQuery += $" {propertyPath} {condition} @{key} ";
        }
        return key;
    }
    private string PureWhere<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, string logic, string condition, int count = 1)
    {
        // Koleksiyonun yolunu alın
        string listPath = ToSingular(GetPropertyPath(list.Body));

        // Koleksiyon eleman özelliğinin yolunu alın
        string propertyPath = GetPropertyPath(prop.Body);
        if (!propertyPath.Contains("."))
        {
            propertyPath = typeof(TEntity).FullName.Split(".").Last() + "." + propertyPath;
        }
        var key = GenerateRandomString();
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += $" {logic} ";
        }
        if (condition == "IN" || condition == "NOT IN")
        {
            ConditionQuery += $" {listPath}.{propertyPath} {condition} (";
            for (int i = 0; i < count; i++)
            {
                ConditionQuery += $"@{key}_{i}";
                if (i < count - 1)
                {
                    ConditionQuery += $",";
                }
            }
            ConditionQuery += $")";
        }
        else
        {
            ConditionQuery += $" {listPath}.{propertyPath} {condition} @{key} ";
        }
        return key;
    }
    private void EmptyWhere<TProperty>(Expression<Func<TEntity, TProperty>> prop, string logic, string condition, string value = null)
    {

        string propertyPath = GetPropertyPath(prop.Body);
        if (!propertyPath.Contains("."))
        {
            propertyPath = typeof(TEntity).FullName.Split(".").Last() + "." + propertyPath;
        }
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += $" {logic} ";
        }
        ConditionQuery += $" {propertyPath} {condition} {(value == null ? "" : $"'{value}'")} ";
    }
    private void EmptyWhere<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, string logic, string condition, string value = null)
    {

        string listPath = ToSingular(GetPropertyPath(list.Body));
        string propertyPath = GetPropertyPath(prop.Body);
        if (!propertyPath.Contains("."))
        {
            propertyPath = typeof(TEntity).FullName.Split(".").Last() + "." + propertyPath;
        }
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += $" {logic} ";
        }
        ConditionQuery += $" {listPath}.{propertyPath} {condition} {(value == null ? "" : $"'{value}'")} ";
    }
    #endregion
    #region Conditions
    public QueryBuilder<TEntity> Where<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "AND", "="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Where<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhere<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "OR", "="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhere<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> WhereNot<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "AND", "<>"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> WhereNot<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "<>"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereNot<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "OR", "<>"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereNot<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "<>"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> WhereIn<TProperty>(Expression<Func<TEntity, TProperty>> prop, IEnumerable<TProperty> values)
    {
        Values.Add(PureWhere(prop, "AND", "IN"), new MultipleItem<TProperty>(values));
        return this;
    }
    public QueryBuilder<TEntity> WhereIn<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "IN"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereIn<TProperty>(Expression<Func<TEntity, TProperty>> prop, IEnumerable<TProperty> values)
    {
        Values.Add(PureWhere(prop, "OR", "IN"), new MultipleItem<TProperty>(values));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereIn<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "IN"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> WhereNotIn<TProperty>(Expression<Func<TEntity, TProperty>> prop, IEnumerable<TProperty> values)
    {
        Values.Add(PureWhere(prop, "AND", "NOT IN"), new MultipleItem<TProperty>(values));
        return this;
    }
    public QueryBuilder<TEntity> WhereNotIn<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "NOT IN"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereNotIn<TProperty>(Expression<Func<TEntity, TProperty>> prop, IEnumerable<TProperty> values)
    {
        Values.Add(PureWhere(prop, "OR", "NOT IN"), new MultipleItem<TProperty>(values));
        return this;
    }
    public QueryBuilder<TEntity> OrWhereNotIn<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "NOT IN"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Empty<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "AND", "=", "");
        return this;
    }
    public QueryBuilder<TEntity> Empty<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "AND", "=", "");
        return this;
    }
    public QueryBuilder<TEntity> OrEmpty<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "OR", "=", "");
        return this;
    }
    public QueryBuilder<TEntity> OrEmpty<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "OR", "=", "");
        return this;
    }
    public QueryBuilder<TEntity> NotEmpty<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "AND", "<>", "");
        return this;
    }
    public QueryBuilder<TEntity> NotEmpty<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "AND", "<>", "");
        return this;
    }
    public QueryBuilder<TEntity> OrNotEmpty<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "OR", "<>", "");
        return this;
    }
    public QueryBuilder<TEntity> OrNotEmpty<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "OR", "<>", "");
        return this;
    }
    public QueryBuilder<TEntity> Null<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "AND", "IS NULL");
        return this;
    }
    public QueryBuilder<TEntity> Null<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "AND", "IS NULL");
        return this;
    }
    public QueryBuilder<TEntity> OrNull<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "OR", "IS NULL");
        return this;
    }
    public QueryBuilder<TEntity> OrNull<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "OR", "IS NULL");
        return this;
    }
    public QueryBuilder<TEntity> NotNull<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "AND", "IS NOT NULL");
        return this;
    }
    public QueryBuilder<TEntity> NotNull<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "AND", "IS NOT NULL");
        return this;
    }
    public QueryBuilder<TEntity> OrNotNull<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        EmptyWhere(prop, "OR", "IS NOT NULL");
        return this;
    }
    public QueryBuilder<TEntity> OrNotNull<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        EmptyWhere(list, prop, "OR", "IS NOT NULL");
        return this;
    }
    public QueryBuilder<TEntity> Greater<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "AND", ">"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Greater<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", ">"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrGreater<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "OR", ">"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrGreater<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", ">"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> GreaterThen<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "AND", ">="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> GreaterThen<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", ">="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrGreaterThen<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "OR", ">="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrGreaterThen<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", ">="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Less<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "AND", "<"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Less<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "<"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrLess<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "OR", "<"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrLess<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "<"), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> LessThen<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "AND", "<="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> LessThen<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "<="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrLessThen<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {

        Values.Add(PureWhere(prop, "OR", "<="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> OrLessThen<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "<="), new SingleItem(value));
        return this;
    }
    public QueryBuilder<TEntity> Between<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty smallValue, TProperty bigValue)
    {
        StartGroup();
        Values.Add(PureWhere(prop, "AND", ">="), new SingleItem(smallValue));
        Values.Add(PureWhere(prop, "AND", "<="), new SingleItem(bigValue));
        EndGroup();
        return this;
    }
    public QueryBuilder<TEntity> Between<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty smallValue, TProperty bigValue)
    {
        StartGroup();
        Values.Add(PureWhere(list, prop, "AND", ">="), new SingleItem(smallValue));
        Values.Add(PureWhere(list, prop, "AND", "<="), new SingleItem(bigValue));
        EndGroup();
        return this;
    }
    public QueryBuilder<TEntity> OrBetween<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty smallValue, TProperty bigValue)
    {
        StartOrGroup();
        Values.Add(PureWhere(prop, "AND", ">="), new SingleItem(smallValue));
        Values.Add(PureWhere(prop, "AND", "<="), new SingleItem(bigValue));
        EndGroup();
        return this;
    }
    public QueryBuilder<TEntity> OrBetween<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty smallValue, TProperty bigValue)
    {
        StartOrGroup();
        Values.Add(PureWhere(list, prop, "AND", ">="), new SingleItem(smallValue));
        Values.Add(PureWhere(list, prop, "AND", "<="), new SingleItem(bigValue));
        EndGroup();
        return this;
    }
    public QueryBuilder<TEntity> Contains<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "AND", "LIKE"), new SingleItem("%" + value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> Contains<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "LIKE"), new SingleItem("%" + value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> OrContains<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "OR", "LIKE"), new SingleItem("%" + value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> OrContains<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "LIKE"), new SingleItem("%" + value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> StartsWith<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "AND", "LIKE"), new SingleItem(value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> StartsWith<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "LIKE"), new SingleItem(value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> OrStartsWith<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "OR", "LIKE"), new SingleItem(value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> OrStartsWith<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "LIKE"), new SingleItem(value + "%"));
        return this;
    }
    public QueryBuilder<TEntity> EndsWith<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "AND", "LIKE"), new SingleItem("%" + value));
        return this;
    }
    public QueryBuilder<TEntity> EndsWith<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "AND", "LIKE"), new SingleItem("%" + value));
        return this;
    }
    public QueryBuilder<TEntity> OrEndsWith<TProperty>(Expression<Func<TEntity, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(prop, "OR", "LIKE"), new SingleItem("%" + value));
        return this;
    }
    public QueryBuilder<TEntity> OrEndsWith<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop, TProperty value)
    {
        Values.Add(PureWhere(list, prop, "OR", "LIKE"), new SingleItem("%" + value));
        return this;
    }
    #endregion
    #region Select
    public QueryBuilder<TEntity> Select<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        string propertyPath = GetPropertyPath(prop.Body);
        if (!propertyPath.Contains("."))
        {
            propertyPath = typeof(TEntity).Name + "." + propertyPath;
        }
        propertyPath = $"{propertyPath} as {propertyPath.Replace(".", "")}";
        if (IsSelectAll == true)
        {
            SelectQuery = propertyPath;
            IsSelectAll = false;
            return this;
        }
        SelectQuery += $", {propertyPath}";
        return this;
    }
    public QueryBuilder<TEntity> SelectAll()
    {
        IsSelectAll = true;
        var nestedName = typeof(TEntity).Name;
        SelectQuery = $"{nestedName}.*";
        return this;
    }
    #endregion
    #region Group
    public QueryBuilder<TEntity> StartGroup()
    {
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += " AND ";
        }
        ConditionQuery += " ( ";
        return this;
    }
    public QueryBuilder<TEntity> StartOrGroup()
    {
        if (ConditionQuery != "" && ConditionQuery.Length > 3 && ConditionQuery.Substring(ConditionQuery.Length - 3) != " ( ")
        {
            ConditionQuery += " OR ";
        }
        ConditionQuery += " ( ";
        return this;
    }
    public QueryBuilder<TEntity> EndGroup()
    {
        ConditionQuery += " ) ";
        return this;

    }
    #endregion
    #region Join
    public QueryBuilder<TEntity> Join<TProperty>(Expression<Func<TEntity, TProperty>> fromProp, Expression<Func<TEntity, TProperty>> joinProp, JoinType joinType)
    {
        string propertyPath = GetPropertyPath(fromProp.Body);
        string propertyPath2 = GetPropertyPath(joinProp.Body);
        var fromNestedName = propertyPath2.Split(".")[0];
        if (joinProp.Body is MemberExpression memberExpression)
        {
            if (memberExpression.Expression is MemberExpression innerMemberExpression)
            {
                Type memberType = GetMemberType(innerMemberExpression);
                JoinTypes.Add(fromNestedName, memberType.GetProperties());
            }
        }
        JoinQuery.Add(fromNestedName, $"{joinType.ToString()} JOIN {ToPlural(fromNestedName)} [{fromNestedName}] ON [{typeof(TEntity).Name}].[{propertyPath}] = [{propertyPath2.Split(".")[0]}].[{propertyPath2.Split(".")[1]}] ");
        return this;
    }
    public QueryBuilder<TEntity> Join<TProperty, TCollection>(
        Expression<Func<TEntity, TProperty>> fromProp,
        Expression<Func<TEntity, IEnumerable<TCollection>>> joinCollectionProp,
        Expression<Func<TCollection, TProperty>> joinKeyProp,
        JoinType joinType)
    {
        // 'fromProp', 'joinCollectionProp' ve 'joinKeyProp' analiz edilecek
        string fromPath = GetPropertyPath(fromProp.Body);
        string collectionName = GetCollectionName(joinCollectionProp.Body);
        string joinKeyPath = GetPropertyPath(joinKeyProp.Body);
        ListJoinQuery.Add(collectionName, $"(SELECT * FROM [{collectionName}] [{ToSingular(collectionName)}] WHERE [{ToSingular(collectionName)}].[{joinKeyPath}] = [{typeof(TEntity).Name}].[{fromPath}] FOR JSON AUTO) as [{collectionName}] ");
        JoinQuery.Add(collectionName, $"{joinType.ToString()} JOIN {collectionName} [{ToSingular(collectionName)}] ON [{typeof(TEntity).Name}].[{fromPath}] = [{ToSingular(collectionName)}].[{joinKeyPath}] ");

        if (!JoinTypes.ContainsKey(collectionName))
        {
            var collectionType = typeof(TCollection);
            JoinTypes.Add(collectionName, collectionType.GetProperties());
        }

        return this;
    }
    #endregion;
    #region Queries
    public IList<TEntity> ToList()
    {
        var result = new List<TEntity>();
        lock (ConnectionStore.SqlConnectionLock)
        {
            using (ConnectionStore.SqlConnection)
            {
                CheckConnection();
                var query = GetQueryString(false);
                using (SqlCommand command = new SqlCommand(query, ConnectionStore.SqlConnection))
                {
                    foreach (var key in Values.Keys)
                    {
                        if (Values[key].GetValues().Count() > 0)
                        {
                            int i = 0;
                            foreach (var item in Values[key].GetValues())
                            {
                                var val = item.ToString();
                                command.Parameters.AddWithValue($"@{key}_{i}", val);
                                i++;
                            }
                        }
                        else
                        {
                            var val = Values[key].GetValue() ?? Values[key].ToString();
                            command.Parameters.AddWithValue($"@{key}", val);
                        }
                    }
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                var entity = new TEntity();
                                entity.LoadFromSqlReader(JoinTypes, reader);
                                var val = entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString();
                                if (val != null)
                                {
                                    if (result.Where(x => x.GetType()?.GetProperty("Id")?.GetValue(x)?.ToString() == val).Any()) continue;
                                }
                                result.Add(entity ?? new());
                            }
                        }
                    }
                }
            }
        }
        Clear();
        return result;
    }
    public TEntity FirstOrDefault()
    {
        LimitValue = 1;
        var entity = new TEntity();
        entity = null;
        var memberName = typeof(TEntity).Name;
        lock (ConnectionStore.SqlConnectionLock)
        {

            using (ConnectionStore.SqlConnection)
            {
                CheckConnection();
                var query = GetQueryString(true);
                using (SqlCommand command = new SqlCommand(query, ConnectionStore.SqlConnection))
                {
                    foreach (var key in Values.Keys)
                    {
                        var val = Values[key].GetValue() ?? Values[key].ToString();
                        command.Parameters.AddWithValue($"@{key}", val);
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                entity = new TEntity();
                                entity.LoadFromSqlReader(JoinTypes, reader);
                                break;
                            }

                        }
                    }
                }
            }
        }
        Clear();
        return entity;
    }
    public TEntity First() => FirstOrDefault() ?? throw new PerfectSqlException("Record not found");
    #endregion;
    private string GetQueryString(bool isSingle)
    {
        string query = "SELECT ";
        var memberName = typeof(TEntity).Name;
        if (IsSelectAll == true)
        {
            query += $"\n 0 as _{memberName}SOL_,";
            foreach (var prop in new TEntity().GetProperties())
            {
                if (prop.PropertyType.IsAssignableTo(typeof(Entity)) || (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))) continue;
                query += $"\n\t [{memberName}].[{prop.Name}] as [{memberName}_{prop.Name}],";
            }
            foreach (string joinTable in ListJoinQuery.Keys)
            {
                var joinQuery = ConditionQuery == "" ? ListJoinQuery[joinTable] : ListJoinQuery[joinTable].Replace("FOR JSON AUTO", $"AND {ConditionQuery} FOR JSON AUTO");
                query += $"\n{joinQuery},";
            }
            query += $"\n 1 as _{memberName}EOL_";
            foreach (string joinTable in JoinQuery.Keys)
            {
                if (ListJoinQuery.Keys.Contains(joinTable)) continue;
                query += $",\n 0 as _{joinTable}SOL_,";

                foreach (var prop in JoinTypes[joinTable])
                {
                    if (prop.PropertyType.IsAssignableTo(typeof(Entity)) || (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))) continue;
                    query += $"\n\t [{joinTable}].[{prop.Name}] as [{joinTable}_{prop.Name}],";
                }
                query += $"\n 1 as _{joinTable}EOL_";
            }
        }
        else
        {
            query += SelectQuery;
        }
        query += $" \nFROM {TableName} [{memberName}]";
        foreach (var joinTable in JoinQuery.Keys)
        {
            query += "\n" + JoinQuery[joinTable];
        }
        if (ConditionQuery != "" && Values.Count > 0)
        {
            query += $" \nWHERE {ConditionQuery} \n";
        }
        if (OrderString.Length > 0)
        {
            query += "\nORDER BY" + OrderString;
        }
        if (LimitValue > 0 || OffsetValue > 0)
        {
            if (OrderString == "")
            {
                query += "\nORDER BY (SELECT 0) ASC";
            }
            query += $"\nOFFSET {OffsetValue} ROWS";
            if (LimitValue > 0)
                query += $"\nFETCH NEXT {LimitValue} ROWS ONLY";
        }
        var printQuery = query;
        foreach (var key in Values.Keys)
        {
            printQuery = printQuery.Replace($"@{key}", $"'{Values[key].GetValue() ?? Values.ToString()}'");
        }
        Debug.WriteLine(printQuery);

        ConditionQuery = "";
        return query;
    }
    protected void CheckConnection()
    {
        if (ConnectionStore.SqlConnection.State != System.Data.ConnectionState.Open || string.IsNullOrEmpty(ConnectionStore.SqlConnection.ConnectionString))
        {
            ConnectionStore.RepairSqlConnection();
            ConnectionStore.SqlConnection.Open();
        }
    }
    #region LimitOperations
    private void SetOrder(string prop, string direction)
    {
        if (OrderString != "")
        {
            OrderString += ",";
        }

        if (!prop.Contains("."))
        {
            prop = typeof(TEntity).FullName.Split(".").Last() + "." + prop;
        }
        OrderString += $"\n {prop} {direction}";
    }
    public QueryBuilder<TEntity> OrderBy<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        var path = GetPropertyPath(prop.Body);
        SetOrder(path, "ASC");
        return this;
    }
    public QueryBuilder<TEntity> OrderBy<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop)
    {
        var listPath = ToSingular(GetPropertyPath(list.Body));
        var path = GetPropertyPath(prop.Body);
        SetOrder($"{listPath}.{path}", "ASC");
        return this;
    }
    public QueryBuilder<TEntity> OrderByDescending<TProperty>(Expression<Func<TEntity, TProperty>> prop)
    {
        var path = GetPropertyPath(prop.Body);
        SetOrder(path, "DESC");
        return this;
    }
    public QueryBuilder<TEntity> OrderByDescending<TCollection, TProperty>(Expression<Func<TEntity, IEnumerable<TCollection>>> list, Expression<Func<TCollection, TProperty>> prop)
    {
        var listPath = ToSingular(GetPropertyPath(list.Body));
        var path = GetPropertyPath(prop.Body);
        SetOrder($"{listPath}.{path}", "DESC");
        return this;
    }
    public QueryBuilder<TEntity> Offset(long offset)
    {
        if (offset < 0)
            throw new PerfectSqlException("Offset cannot be less than zero");
        OffsetValue = offset;
        return this;
    }
    public QueryBuilder<TEntity> Limit(long limit)
    {
        if (limit < 0)
            throw new PerfectSqlException("Limit cannot be less than zero");
        LimitValue = limit;
        return this;
    }
    #endregion;
    public void Add<T>(T model)
        where T : class, new()
    {
        string query = $"INSERT INTO {TableName} ({model.GetPropertiesSqlString()}) OUTPUT INSERTED.Id  VALUES ({model.GetValuesSqlKeyString()})";
        RunCommand(query, model);
    }
    public void Add<TModel, TRule>(TModel model, TRule rules)
        where TModel : class, new()
        where TRule : BaseDMLRule<TModel>, new()
    {
        rules.Run(model);
        Add(model);
    }
    public void Update<T>(T model)
        where T : class, new()
    {
        string query = $"Update {TableName} SET {model.GetValuesUpdateSqlKeyString()} WHERE Id=@Id";
        RunCommand(query, model);
    }
    public void Update<TModel, TRule>(TModel model, TRule rules)
        where TModel : class, new()
        where TRule : BaseDMLRule<TModel>, new()
    {
        rules.Run(model);
        Update(model);
    }
    public void DeleteById(string id)
    {
        var deleteQuery = $"DELETE FROM {TableName} WHERE Id='{id}'";
        RunCommand<TEntity>(deleteQuery, null);
    }
    public void Migrate()
    {
        DatabaseMigrate();
        var entity = new TEntity();
        var newSchema = entity.GetCreateTablePropertySqlString();

        // Mevcut tablo şemasını al
        var existingSchema = GetExistingTableSchema(TableName);

        if (string.IsNullOrEmpty(existingSchema))
        {
            // Tablo yoksa, oluştur
            var createTableQuery = $@"CREATE TABLE [dbo].[{TableName}]({newSchema})";
            RunCommand<TEntity>(createTableQuery, null);
        }
        else
        {
            // Tablo varsa, farklılıkları uygula
            var alterCommands = GetSchemaDifferences(existingSchema, newSchema);
            foreach (var command in alterCommands)
            {
                RunCommand<TEntity>(command, null);
            }
        }
    }
    protected void RunCommand<T>(string query, T model)
        where T : class, new()
    {
        lock (ConnectionStore.SqlConnectionLock)
        {

            using (ConnectionStore.SqlConnection)
            {
                CheckConnection();
                using (SqlCommand command = new SqlCommand(query, ConnectionStore.SqlConnection))
                {
                    if (model != null)
                    {
                        var props = model.GetProperties().Where(x => x.Name != "Id").ToArray();
                        foreach (var prop in props)
                        {
                            if (prop.PropertyType.IsAssignableTo(typeof(Entity)) || (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))) continue;
                            var value = prop.GetValue(model, null);
                            if (prop.PropertyType == typeof(DateTime))
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", (value as DateTime?)?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL");
                            }
                            else if (prop.PropertyType == typeof(TimeSpan))
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", (value as TimeSpan?)?.ToString("HH:mm:ss.fff") ?? "");
                            }
                            else if (prop.PropertyType == typeof(bool))
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", (value == null || (bool)value == false) ? "0" : "1");
                            }
                            else if (prop.PropertyType == typeof(float)
                                || prop.PropertyType == typeof(double)
                                || prop.PropertyType == typeof(long)
                                || prop.PropertyType == typeof(int)
                                || prop.PropertyType == typeof(short))
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", value ?? 0);
                            }
                            else if (prop.PropertyType == typeof(uint))
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", (long)(value ?? (uint)0));
                            }
                            else if (prop.PropertyType == typeof(ushort))
                            {
                                value = value ?? 0;
                                command.Parameters.AddWithValue($"@{prop.Name}", int.Parse(value.ToString()));
                            }
                            else
                            {
                                command.Parameters.AddWithValue($"@{prop.Name}", value?.ToString() ?? "");
                            }
                        }
                        if (model.GetProperties().Where(x => x.Name == "Id").Any())
                        {
                            var prop = model.GetProperties().Where(x => x.Name == "Id").First();
                            var value = (Guid?)prop.GetValue(model) ?? Guid.Empty;
                            if (value == Guid.Empty)
                            {
                                value = Guid.NewGuid();
                                prop.SetValue(model, value);
                            }
                            command.Parameters.AddWithValue($"@{prop.Name}", value);
                        }
                    }
                    command.ExecuteNonQuery();
                }

            }
        }
    }
    private string GetExistingTableSchema(string tableName)
    {
        string query = $@"
        SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = '{tableName}'";

        lock (ConnectionStore.SqlConnectionLock)
        {

            using (ConnectionStore.SqlConnection)
            {
                CheckConnection();
                using (var command = new SqlCommand(query, ConnectionStore.SqlConnection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        var schema = new List<string>();
                        while (reader.Read())
                        {
                            var columnName = reader["COLUMN_NAME"].ToString();
                            var dataType = reader["DATA_TYPE"].ToString();
                            var isNullable = reader["IS_NULLABLE"].ToString() == "YES" ? "NULL" : "NOT NULL";
                            schema.Add($"{columnName} {dataType} {isNullable}");
                        }
                        return string.Join(", ", schema);
                    }
                }
            }
        }
    }
    private List<string> GetSchemaDifferences(string existingSchema, string newSchema)
    {
        var alterCommands = new List<string>();

        // Mevcut ve yeni sütunları parse et
        var existingColumns = existingSchema.Split(new[] { ", " }, StringSplitOptions.None).Select(x => x.Replace("\n", "").Replace("\t", ""))
            .Select(c => NormalizeColumn(c))
            .ToDictionary(c => c.ColumnName, c => c);
        var newColumns = newSchema.Split(new[] { ", " }, StringSplitOptions.None)
            .Select(c => NormalizeColumn(c))
            .ToDictionary(c => c.ColumnName, c => c);

        // Yeni sütunlar için kontrol
        foreach (var newColumn in newColumns)
        {
            if (!existingColumns.ContainsKey(newColumn.Key))
            {
                alterCommands.Add($"ALTER TABLE [dbo].[{TableName}] ADD {newColumn.Value.RawDefinition}");
            }
            else if (existingColumns[newColumn.Key].DataType != newColumn.Value.DataType)
            {
                // Veri tipi değişikliği için ALTER COLUMN
                alterCommands.Add($"ALTER TABLE [dbo].[{TableName}] ALTER COLUMN {newColumn.Value.RawDefinition}");
            }
        }

        // Eksik sütunlar için kontrol
        foreach (var existingColumn in existingColumns)
        {
            if (!newColumns.ContainsKey(existingColumn.Key))
            {
                alterCommands.Add($"ALTER TABLE [dbo].[{TableName}] DROP COLUMN {existingColumn.Key}");
            }
        }

        return alterCommands;
    }
    private (string ColumnName, string DataType, string RawDefinition) NormalizeColumn(string columnDefinition)
    {
        var parts = columnDefinition.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var columnName = parts.Count() < 1 ? "" : parts[0].Replace("\n", "").Replace("\t", "");
        var dataType = parts.Count() < 2 ? "" : parts[1];

        // nvarchar vs nvarchar(MAX) farkını normalize edin
        if (dataType == "nvarchar(MAX)")
        {
            dataType = dataType.Replace("(MAX)", ""); // Hepsini "nvarchar" olarak normalize et
        }

        return (columnName, dataType, columnDefinition.Trim());
    }
    private void DatabaseMigrate()
    {
        using (SqlConnection sqlConnection = ConnectionStore.MasterSqlConnection)
        {
            sqlConnection.Open();
            string query = $@"IF DB_ID('{ConnectionStore.DatabaseName}') IS NULL
                    BEGIN
                        CREATE DATABASE {ConnectionStore.DatabaseName};
                        PRINT 'Database created successfully.';
                    END
                    ELSE
                    BEGIN
                        PRINT 'Database already exists.';
                    END
                    ";
            using (SqlCommand command = new(query, sqlConnection))
            {
                command.ExecuteNonQuery();
            }
            sqlConnection.Close();
        }
    }
}
