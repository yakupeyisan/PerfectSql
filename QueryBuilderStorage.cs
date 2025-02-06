using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Text.Json;

namespace PerfectSql;

internal static class QueryBuilderStorage
{
    public static IDictionary<string, PropertyInfo[]> PropertyCollection = new Dictionary<string, PropertyInfo[]>();
    public static object PropertyCollectionLock = new object();
    public static PropertyInfo[] GetProperties<T>(this T model)
    where T : class
    {
        if (model == null) throw new Exception("Model not nullable");
        lock (PropertyCollectionLock)
        {
            var key = model.GetType().FullName;
            if (PropertyCollection.ContainsKey(key))
            {
                return PropertyCollection[key];
            }
            var values = model.GetType().GetProperties();
            PropertyCollection.Add(key, values);
            return values;
        }
    }
    public static string GetPropertiesSqlString<T>(this T model)
        where T : class
    {
        return string.Join(", ", model.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(Entity)) == false && (x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)) == false).Select(x => x.Name).ToArray());
    }
    public static string GetValuesSqlKeyString<T>(this T model)
        where T : class
    {
        var properties = model.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(Entity)) == false && (x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)) == false).ToArray();
        string[] values = new string[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            values[i] = "@" + prop.Name;
        }
        return $"{string.Join(", ", values)}";
    }
    public static string GetValuesUpdateSqlKeyString<T>(this T model)
        where T : class
    {
        var properties = model.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(Entity)) == false && x.Name != "Id" && (x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)) == false).ToArray();
        string[] values = new string[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            values[i] = prop.Name + "= @" + prop.Name;
        }
        return $"{string.Join(", ", values)}";
    }
    public static string GetValuesSqlString<T>(this T model)
        where T : class
    {
        var properties = model.GetProperties();
        string[] values = new string[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            var value = prop.GetValue(model, null);
            if (prop.PropertyType == typeof(DateTime))
            {
                values[i] = (value as DateTime?)?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            }
            else if (prop.PropertyType == typeof(TimeSpan))
            {
                values[i] = (value as TimeSpan?)?.ToString("HH:mm:ss.fff") ?? "";
            }
            else if (prop.PropertyType == typeof(bool))
            {
                values[i] = (value == null || (bool)value == false) ? "0" : "1";
            }
            else
            {
                values[i] = value?.ToString() ?? "";
            }
        }
        return $"'{string.Join("', '", values)}'";
    }
    public static string GetCreateTablePropertySqlString<T>(this T model)
        where T : class
    {
        string ret = "";
        var properties = model.GetProperties();
        var columnNames = new HashSet<string>(); // Eklenen sütunları takip etmek için

        foreach (var prop in properties)
        {
            if (prop.PropertyType.IsAssignableTo(typeof(Entity)) ||
                (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
                continue;

            if (columnNames.Contains(prop.Name)) continue; // Aynı isimde sütun varsa atla
            columnNames.Add(prop.Name);

            if (ret != "") ret += ", ";
            ret += "\n\t\t" + prop.Name + " ";

            string type = "nvarchar(MAX)";
            if (prop.PropertyType == typeof(Guid)) type = "uniqueidentifier";
            else if (prop.PropertyType == typeof(DateTime)) type = "datetime";
            else if (prop.PropertyType == typeof(TimeSpan)) type = "time(7)";
            else if (prop.PropertyType == typeof(ulong) || prop.PropertyType == typeof(long)) type = "bigint";
            else if (prop.PropertyType == typeof(int)) type = "int";
            else if (prop.PropertyType == typeof(uint)) type = "bigint";
            else if (prop.PropertyType == typeof(short)) type = "short";
            else if (prop.PropertyType == typeof(ushort)) type = "int";
            else if (prop.PropertyType == typeof(byte)) type = "tinyint";
            else if (prop.PropertyType == typeof(float)) type = "float";
            else if (prop.PropertyType == typeof(double)) type = "float";
            else if (prop.PropertyType == typeof(decimal)) type = "decimal(18,2)";

            ret += type;
            ret += " " + ((IsNullable(prop)) ? "NULL" : "NOT NULL");
        }

        return ret;
    }
    static bool IsNullable(PropertyInfo propertyInfo)
    {
        var nullableAttribute = propertyInfo.PropertyType.CustomAttributes;

        // Eğer PropertyType ValueType değilse ve Nullable özellik içermiyorsa
        if (!propertyInfo.PropertyType.IsValueType)
        {
            return propertyInfo.PropertyType.IsGenericType &&
                   propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        // Referans tipi için Nullable attribute'u kontrol edelim (C# 8+ için geçerli)
        var nullable = propertyInfo
                        .CustomAttributes
                        .Any(a => a.AttributeType == typeof(System.Runtime.CompilerServices.NullableAttribute));

        return nullable;
    }

    public static TEntity LoadFromSqlReader<TEntity>(this TEntity entity, IDictionary<string, PropertyInfo[]> JoinTypes, SqlDataReader reader)
    {
        var memberName = typeof(TEntity).Name;
        string tbl = "";
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
            if (val == null) continue;
            if (reader.GetName(i).StartsWith("_") && (int)val == 0)
            {
                tbl = reader.GetName(i).Replace("SOL_", "").Replace("_", "");
                continue;
            }
            if (reader.GetName(i).StartsWith("_") && (int)val == 1)
            {
                tbl = "";
                continue;
            }
            if (tbl == memberName)
            {
                string propName = reader.GetName(i).Replace($"{tbl}_", "");
                var prop = entity?.GetType()?.GetProperty(propName);
                if (prop == null) continue;
                if (JoinTypes.ContainsKey(propName))
                {
                    if (val == null) continue;
                    var listVal = JsonSerializer.Deserialize((string)val, prop.PropertyType);
                    prop.SetValue(entity, listVal);

                    continue;
                }
                if (prop.PropertyType == typeof(bool))
                {
                    val = byte.Parse(val.ToString()) == 1;
                }
                var castVal = val == null ? null : Convert.ChangeType(val, prop.PropertyType);
                prop.SetValue(entity, castVal);
            }
            else
            {
                string propName = reader.GetName(i).Replace($"{tbl}_", "");
                var nestedObject = entity?.GetType()?.GetProperty(tbl)?.GetValue(entity);
                if (nestedObject == null)
                {
                    nestedObject = Activator.CreateInstance(entity?.GetType()?.GetProperty(tbl)?.PropertyType);
                    entity?.GetType()?.GetProperty(tbl)?.SetValue(entity, nestedObject);
                }
                var baseProp = entity?.GetType()?.GetProperty(tbl);
                var prop = nestedObject?.GetType()?.GetProperty(propName);
                if (prop.PropertyType == typeof(bool))
                {
                    val = val.ToString() == "1" ? "True" : val;
                    val = val.ToString() == "0" ? "False" : val;
                    var castVal = val == null ? null : Convert.ChangeType(val, prop.PropertyType);
                    prop.SetValue(nestedObject, castVal);
                }
                else
                {
                    var castVal = val == null ? null : Convert.ChangeType(val, prop.PropertyType);
                    prop.SetValue(nestedObject, castVal);
                }
            }
        }
        return entity;
    }
}
