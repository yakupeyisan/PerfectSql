# PerfectSql

**PerfectSql** MSSQL için dinamik sorgular oluþturmanýzý saðlayan güçlü ve esnek bir kütüphanedir. Projenizde hýzlý ve esnek sorgular yazmanýza yardýmcý olur.

---

## Özellikler
- **Dinamik Sorgular:** MSSQL sorgularýný kolayca oluþturmanýza olanak tanýr.
- **Hýzlý Entegrasyon:** Kütüphaneyi projeye ekleyerek hemen kullanmaya baþlayabilirsiniz.
- **Esneklik:** Migration, sorgu yapýcý (query builder) ve diðer veri tabaný iþlemleri için uygundur.

---

## Kurulum
PerfectSql, NuGet üzerinden kolayca projeye dahil edilebilir. Ýlgili .NET sürümüne göre aþaðýdaki komutlardan birini kullanabilirsiniz:

### .NET 8.0 Ýçin Kurulum
.NET CLI kullanarak aþaðýdaki komutu çalýþtýrýn:
```bash
 dotnet add package PerfectSql --version 2.0.0
```

### .NET 9.0 Ýçin Kurulum
.NET CLI kullanarak aþaðýdaki komutu çalýþtýrýn:
```bash
 dotnet add package PerfectSql --version 2.1.8
```

---

PerfectSql ile MSSQL veritabaný iþlemlerinizi daha verimli ve esnek hale getirebilirsiniz!

---

# PerfectSql.QueryBuilder

`PerfectSql.QueryBuilder`, .NET uygulamalarýnda SQL sorgularýný dinamik olarak oluþturmak ve yönetmek için kullanýlan bir kütüphanedir. Bu kütüphane, LINQ benzeri bir sözdizimi kullanarak SQL sorgularýný kolayca oluþturmanýza ve çalýþtýrmanýza olanak tanýr. Ayrýca, CRUD iþlemlerini ve veritabaný migrasyonlarýný destekler.

## Kurulum

NuGet paket yöneticisi konsolunu kullanarak paketi projenize ekleyebilirsiniz:

```bash
NuGet\Install-Package PerfectSql -Version 2.1.8
```

## Baþlarken

### Temel Kullaným

`QueryBuilder<TEntity>` sýnýfý, bir varlýk (entity) türü üzerinde çalýþýr ve bu varlýk için SQL sorgularý oluþturur. Aþaðýda temel bir örnek bulunmaktadýr:

```csharp
using PerfectSql;

public class User : Entity
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class Program
{
    public static void Main()
    {
        ConnectionStore.SetConnectionString("connection string"); 
        var users = QueryBuilder<User>.Instance
            .Where(u => u.Age, 25)
            .ToList();

        foreach (var user in users)
        {
            Console.WriteLine($"Name: {user.Name}, Age: {user.Age}");
        }
    }
}
```

### Sorgu Oluþturma

#### WHERE Koþullarý

`QueryBuilder<TEntity>` sýnýfý, çeþitli WHERE koþullarý eklemenize olanak tanýr:

```csharp
var users = QueryBuilder<User>.Instance
    .Where(u => u.Age, 25)
    .OrWhere(u => u.Name, "John")
    .WhereNot(u => u.Age, 30)
    .ToList();
```

#### JOIN Ýþlemleri

JOIN iþlemleri, iliþkili tablolarý birleþtirmek için kullanýlýr:

```csharp
var users = QueryBuilder<User>.Instance
    .Join(u => u.Id, u => u.Address.UserId, JoinType.Inner)
    .ToList();
```

#### Sýralama ve Sayfalama

Sorgu sonuçlarýný sýralamak ve sayfalamak için aþaðýdaki yöntemleri kullanabilirsiniz:

```csharp
var users = QueryBuilder<User>.Instance
    .OrderBy(u => u.Name)
    .Offset(10)
    .Limit(5)
    .ToList();
```

### CRUD Ýþlemleri

#### Ekleme (Insert)

Yeni bir kayýt eklemek için `Add` yöntemini kullanýn:

```csharp
var newUser = new User { Name = "Alice", Age = 30 };
QueryBuilder<User>.Instance.Add(newUser);
```

#### Güncelleme (Update)

Mevcut bir kaydý güncellemek için `Update` yöntemini kullanýn:

```csharp
var userToUpdate = new User { Id = 1, Name = "Bob", Age = 35 };
QueryBuilder<User>.Instance.Update(userToUpdate);
```

#### Silme (Delete)

Bir kaydý silmek için `DeleteById` yöntemini kullanýn:

```csharp
QueryBuilder<User>.Instance.DeleteById("1");
```

### Veritabaný Migrasyonlarý

Veritabaný þemasýný güncellemek için `Migrate` yöntemini kullanýn:

```csharp
QueryBuilder<User>.Instance.Migrate();
```

Bu yöntem, varlýk sýnýfýnýzýn özelliklerine göre veritabaný tablosunu otomatik olarak oluþturur veya günceller.

## Geliþmiþ Özellikler

### Dinamik Sorgu Oluþturma

`QueryBuilder<TEntity>` sýnýfý, dinamik olarak sorgular oluþturmanýza olanak tanýr. Örneðin, kullanýcý girdisine göre farklý WHERE koþullarý ekleyebilirsiniz:

```csharp
var query = QueryBuilder<User>.Instance;

if (someCondition)
{
    query = query.Where(u => u.Age, 25);
}

if (anotherCondition)
{
    query = query.Where(u => u.Name, "John");
}

var users = query.ToList();
```

### JSON Desteði

`QueryBuilder<TEntity>`, JSON formatýnda veri döndürmeyi destekler. Özellikle, iliþkili koleksiyonlarý JSON olarak almak için kullanýlabilir:

```csharp
var users = QueryBuilder<User>.Instance
    .Join(u => u.Id, u => u.Address.UserId, JoinType.Inner)
    .ToList();
```

## Örnekler

### Temel Sorgu Örneði

```csharp
var users = QueryBuilder<User>.Instance
    .Where(u => u.Age > 18)
    .OrderBy(u => u.Name)
    .Limit(10)
    .ToList();
```

### JOIN ile Sorgu Örneði

```csharp
var users = QueryBuilder<User>.Instance
    .Join(u => u.Id, u => u.Address.UserId, JoinType.Inner)
    .Where(u => u.Address.City, "New York")
    .ToList();
```

### CRUD Ýþlemleri Örneði

```csharp
// Ekleme
var newUser = new User { Name = "Alice", Age = 30 };
QueryBuilder<User>.Instance.Add(newUser);

// Güncelleme
var userToUpdate = new User { Id = 1, Name = "Bob", Age = 35 };
QueryBuilder<User>.Instance.Update(userToUpdate);

// Silme
QueryBuilder<User>.Instance.DeleteById("1");
```

### Veritabaný Baðlantý Kontrolü

```csharp
ConnectionStore.SetConnectionString("connectionstring");

//2.0.3 ve sonrasýnda geçerli
ConnectionStore.CheckDatabaseConnection();
```


### DML öncesi kurallar eklendi.

```csharp
//2.1.0 ve sonrasýnda geçerli
var rules= new UserRules();

// Ekleme
var newUser = new User { Name = "Alice", Age = 12 };
QueryBuilder<User>.Instance.Add(newUser,rules);

// Güncelleme
var userToUpdate = new User { Id = 1, Name = "Bob", Age = 17 };
QueryBuilder<User>.Instance.Update(userToUpdate,rules);

public class UserRules : BaseDMLRule<User>
{
    public UserRules()
    {
        AddRule(x => string.IsNullOrEmpty(x.Name));
        AddRule(x => x.Age <18,"Kiþi minimum 18 yaþýnda olmalýdýr.");
    }
}

```

### List ekleme

```csharp
//Listeden Ekleme
var newUserList=new List<User>()
{
    new(){ Name = "Alice", Age = 30 },
    new(){ Name = "Bob", Age = 30 }
};
QueryBuilder<User>.Instance.Add(newUserList);

QueryBuilder<AutoClean>.Instance.Add(list,(AutoClean record,Exception ex) =>
{
    //Kayýt eklenirken alýnan hatalar yer alýyor
});


```
### Queue(kuyruk) üzerinden ekleme

```csharp
//Queue Ekleme
new Task(async () =>
{
    var cancellationToken = new CancellationTokenSource();
    new Task(async () => await QueryBuilder<User>.Instance.Add(queue, cancellationToken.Token)).Start();
    while (true)
    {
        queue.Enqueue(new AutoClean()
        {
            Name = "Test",
             Age = 30

        });
        await Task.Delay(10);
    }

}).Start();


```


## Lisans

Bu proje MIT lisansý altýnda lisanslanmýþtýr. Daha fazla bilgi için [LICENSE](LICENSE) dosyasýna bakýn.

---


## Versiyon Notlarý
2.0.2 FirstOrDefault Sorgusunda oluþan hata düzeltildi

---
2.0.3 ConnectionStore ile veritabaný balantý kontrolü eklendi.

---
2.0.4 UShort column hatasý giderildi.

---
2.0.5 Boolean cast hatasý giderildi.

---
2.0.6 Otomatik sorgu oluþturu düzenlendi.

---
2.0.7 Join query list hatasý düzeltildi.

---
2.1.0 DML öncesi model kontrolü eklendi.

---
2.1.1 Join query sorgu hatasý düzeltildi.Part#1

---
2.1.2 Join query sorgu hatasý düzeltildi. Part#2 Namespace kaynaklý hata giderildi.

---
2.1.3 UnaryExpression desteði eklendi.

---
2.1.4 Bug fix giderildi.

---
2.1.5 Liste olarak ekleme ve queue ye abone olup ekleme eklendi


---
2.1.6 Update column default value bug fix

---
2.1.7 BaseDmlRules içerisine UnaryExpression desteði eklendi.

---
2.1.8 BaseDmlRules içerisine MethodCallExpression desteði eklendi.
