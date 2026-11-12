using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using VetWeb.Data;

internal static class ModeloPruebas
{
    public static async Task EjecutarAsync(ApplicationDbContext db)
    {
        if (db.Database.HasPendingModelChanges()) throw new Exception("El modelo tiene cambios pendientes de migración.");
        var migrations = db.Database.GetMigrations().ToArray();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        if (!migrations.SequenceEqual(applied)) throw new Exception("El historial aplicado no coincide con las migraciones del proyecto.");
        try
        {
            var model=db.GetService<IDesignTimeModel>().Model;
            var relational=model.GetRelationalModel();
            await db.Database.OpenConnectionAsync();
            var sql=(NpgsqlConnection)db.Database.GetDbConnection();
            async Task<List<string[]>> Rows(string query)
            {
             await using var command=new NpgsqlCommand(query,sql); await using var reader=await command.ExecuteReaderAsync();
             var rows=new List<string[]>(); while(await reader.ReadAsync())rows.Add(Enumerable.Range(0,reader.FieldCount).Select(i=>reader.IsDBNull(i)?"":reader.GetValue(i).ToString()!).ToArray());return rows;
            }
            void Same(IEnumerable<string> expected,IEnumerable<string> actual,string label)
            {
             var a=expected.Order().ToArray();var b=actual.Order().ToArray();
             if(!a.SequenceEqual(b))throw new Exception($"{label}: faltan [{string.Join(';',a.Except(b))}], sobran [{string.Join(';',b.Except(a))}].");
            }
            var tables=await Rows("SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename<>'__EFMigrationsHistory'");
            Same(relational.Tables.Select(t=>t.Name),tables.Select(r=>r[0]),"Tablas");
            var columns=await Rows("""
             SELECT c.relname,a.attname,format_type(a.atttypid,a.atttypmod),NOT a.attnotnull
             FROM pg_attribute a JOIN pg_class c ON a.attrelid=c.oid JOIN pg_namespace n ON c.relnamespace=n.oid
             WHERE n.nspname='public' AND c.relkind='r' AND c.relname<>'__EFMigrationsHistory' AND a.attnum>0 AND NOT a.attisdropped
             """);
            Same(relational.Tables.SelectMany(t=>t.Columns.Select(c=>$"{t.Name}|{c.Name}|{c.StoreType}|{c.IsNullable}")),columns.Select(r=>string.Join('|',r)),"Columnas, tipos y nulabilidad");
            var primary=await Rows("""
             SELECT c.relname,k.conname,string_agg(a.attname,',' ORDER BY x.ord)
             FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
             CROSS JOIN LATERAL unnest(k.conkey) WITH ORDINALITY AS x(num,ord) JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=x.num
             WHERE n.nspname='public' AND k.contype='p' AND c.relname<>'__EFMigrationsHistory' GROUP BY c.relname,k.conname
             """);
            Same(relational.Tables.Where(t=>t.PrimaryKey!=null).Select(t=>$"{t.Name}|{t.PrimaryKey!.Name}|{string.Join(',',t.PrimaryKey.Columns.Select(c=>c.Name))}"),primary.Select(r=>string.Join('|',r)),"Claves primarias");
            var foreign=await Rows("""
             SELECT c.relname,k.conname,p.relname,k.confdeltype::text,
             string_agg(a.attname,',' ORDER BY x.ord),string_agg(b.attname,',' ORDER BY x.ord)
             FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_class p ON p.oid=k.confrelid
             CROSS JOIN LATERAL unnest(k.conkey,k.confkey) WITH ORDINALITY AS x(num,pnum,ord)
             JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=x.num JOIN pg_attribute b ON b.attrelid=p.oid AND b.attnum=x.pnum
             WHERE n.nspname='public' AND k.contype='f' GROUP BY c.relname,k.conname,p.relname,k.confdeltype
             """);
            string DeleteCode(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction action)=>action switch
            {
             Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade=>"c",Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict=>"r",
             Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull=>"n",Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetDefault=>"d",_=>"a"
            };
            Same(relational.Tables.SelectMany(t=>t.ForeignKeyConstraints.Select(f=>$"{t.Name}|{f.Name}|{f.PrincipalTable.Name}|{DeleteCode(f.OnDeleteAction)}|{string.Join(',',f.Columns.Select(c=>c.Name))}|{string.Join(',',f.PrincipalColumns.Select(c=>c.Name))}")),foreign.Select(r=>string.Join('|',r)),"FK y políticas de borrado");
            var indexes=await Rows("""
             SELECT t.relname,i.relname,x.indisunique,string_agg(a.attname,',' ORDER BY col.ord)
             FROM pg_index x JOIN pg_class t ON t.oid=x.indrelid JOIN pg_class i ON i.oid=x.indexrelid JOIN pg_namespace n ON n.oid=t.relnamespace
             CROSS JOIN LATERAL unnest(x.indkey) WITH ORDINALITY AS col(num,ord) JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=col.num
             WHERE n.nspname='public' AND t.relname<>'__EFMigrationsHistory' AND NOT x.indisprimary GROUP BY t.relname,i.relname,x.indisunique
             """);
            Same(relational.Tables.SelectMany(t=>t.Indexes.Select(i=>$"{t.Name}|{i.Name}|{i.IsUnique}|{string.Join(',',i.Columns.Select(c=>c.Name))}")),indexes.Select(r=>string.Join('|',r)),"Índices y unicidad");
            var checks=await Rows("""
             SELECT c.relname,k.conname,pg_get_constraintdef(k.oid)
             FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname='public' AND k.contype='c'
             """);
            Same(relational.Tables.SelectMany(t=>t.CheckConstraints.Select(c=>$"{t.Name}|{c.Name}")),checks.Select(r=>$"{r[0]}|{r[1]}"),"Restricciones CHECK");
            foreach (var table in relational.Tables)
            foreach (var constraint in table.CheckConstraints)
            {
                var quotedTable = "\"" + table.Name.Replace("\"", "\"\"") + "\"";
                await using var create = new NpgsqlCommand($"CREATE TEMP TABLE vetweb_modelo_check (LIKE public.{quotedTable})", sql);
                await create.ExecuteNonQueryAsync();
                try
                {
                    // El SQL proviene del modelo EF, nunca de una solicitud.
                    await using var add = new NpgsqlCommand($"ALTER TABLE pg_temp.vetweb_modelo_check ADD CONSTRAINT esperado CHECK ({constraint.Sql})", sql);
                    await add.ExecuteNonQueryAsync();
                    var canonical = (await Rows("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='pg_temp.vetweb_modelo_check'::regclass AND conname='esperado'")).Single()[0];
                    var actual = checks.Single(x => x[0] == table.Name && x[1] == constraint.Name)[2];
                    if (canonical != actual) throw new Exception($"La expresión de {constraint.Name} difiere del modelo.");
                }
                finally
                {
                    await using var drop = new NpgsqlCommand("DROP TABLE pg_temp.vetweb_modelo_check", sql);
                    await drop.ExecuteNonQueryAsync();
                }
            }
            var der=File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../docs/DER.md")));
            var diagram=Regex.Matches(der,@"(?m)^\s*(\w+)\s+(\|\||o\|)--(o\{|o\|)\s+(\w+)\s*:")
             .Select(m=>$"{m.Groups[1].Value}|{m.Groups[4].Value}|{m.Groups[2].Value}|{m.Groups[3].Value}");
            var domain=model.GetEntityTypes().Where(t=>t.ClrType.Namespace=="VetWeb.Models").SelectMany(t=>t.GetForeignKeys())
             .Where(f=>f.PrincipalEntityType.ClrType.Namespace=="VetWeb.Models")
             .Select(f=>$"{f.PrincipalEntityType.ClrType.Name}|{f.DeclaringEntityType.ClrType.Name}|{(f.IsRequired?"||":"o|")}|{(f.IsUnique?"o|":"o{")}");
            Same(domain,diagram,"Relaciones y cardinalidades DER");
            Console.WriteLine($"Modelo y DER: {migrations.Length} migraciones, {tables.Count} tablas, {columns.Count} columnas, {primary.Count} PK, {foreign.Count} FK, {indexes.Count} índices, {checks.Count} CHECK y {diagram.Count()} relaciones verificados.");
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
