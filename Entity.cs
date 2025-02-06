using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.PortableExecutable;
using System.Xml;

namespace PerfectSql;
public class Entity
{
}
public class Entity<T> : Entity
    where T : notnull
{
    public T Id { get; set; }
}
