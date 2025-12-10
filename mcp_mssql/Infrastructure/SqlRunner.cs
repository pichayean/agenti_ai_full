using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MssqlMcpServer.Infrastructure;

public sealed class SqlRunner
{
    private readonly DatabaseOptions _opts;

    public SqlRunner(DatabaseOptions opts) => _opts = opts;

    private SqlConnection Create() => new SqlConnection(_opts.ConnectionString);

    // ---------- 1) Check DB connectivity ----------
    public async Task<object> CheckConnectivityAsync()
    {
        using var conn = Create();
        await conn.OpenAsync();
        var db = await conn.ExecuteScalarAsync<string>("SELECT DB_NAME();");
        var ver = await conn.ExecuteScalarAsync<string>("SELECT @@VERSION;");
        return new { ok = true, database = db, serverVersion = ver };
    }

    // ---------- 2) Run SELECT with params + pagination (ROW_NUMBER) ----------

    public async Task<object> RunSelectAsync(RunSelectRequest req)
    {
        if (req is null) throw new ArgumentNullException(nameof(req));
        var sql = req.Sql?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("Sql is required.", nameof(req.Sql));

        if (!Regex.IsMatch(sql, @"^\s*SELECT\b", RegexOptions.IgnoreCase))
            throw new ArgumentException("Only SELECT statements are allowed.", nameof(req.Sql));
        if (sql.Contains(';'))
            throw new ArgumentException("Multiple statements are not allowed (no ';').", nameof(req.Sql));

        var page = req.Page < 1 ? 1 : req.Page;
        var pageSize = req.PageSize < 1 ? 1 : (req.PageSize > 50 ? 50 : req.PageSize);

        var dyn = new DynamicParameters();
        if (req.Params is not null)
        {
            foreach (var kv in req.Params)
            {
                var normalized = RunSelectHelper.NormalizeParamValue(kv.Value, out DbType dbType);
                dyn.Add(kv.Key, normalized, dbType);
            }
        }

        using var conn = Create();
        await conn.OpenAsync();
        var raw = await conn.QueryAsync(sql, dyn, commandTimeout: _opts.CommandTimeoutSeconds);
        var rows = raw.AsList(); // หรือ raw.ToList()

        return new { page, pageSize, rows };
    }


    public async Task<object> RunProcedureAsync(RunProcedureRequest req)
    {
        if (req is null) throw new ArgumentNullException(nameof(req));
        var procName = req.ProcedureName?.Trim();
        if (string.IsNullOrWhiteSpace(procName))
            throw new ArgumentException("ProcedureName is required.", nameof(req.ProcedureName));

        // ป้องกันชื่อโปรซีเยอร์เบื้องต้น (รองรับ schema เช่น dbo.Proc, และวงเล็บเหลี่ยม)
        if (!Regex.IsMatch(procName, @"^[\[\]A-Za-z0-9_.]+$"))
            throw new ArgumentException("Invalid stored procedure name.", nameof(req.ProcedureName));

        var dyn = new DynamicParameters();

        // รับพารามิเตอร์แบบ input จาก req.Params (Dictionary<string, object>)
        if (req.Params is not null)
        {
            foreach (var kv in req.Params)
            {
                var name = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // ชื่อพารามิเตอร์ของ Dapper ไม่จำเป็นต้องมี '@' นำหน้า แต่เผื่อผู้ใช้ส่งมาก็ตัดออกให้
                if (name.StartsWith("@")) name = name.Substring(1);

                // ใช้ตัวช่วยเดิมเพื่อ map ไปเป็น DbType ที่เหมาะสม
                var normalized = RunSelectHelper.NormalizeParamValue(kv.Value, out DbType dbType);
                dyn.Add(name, normalized, dbType, direction: ParameterDirection.Input);
            }
        }

        // ถ้าต้องการรับ return value จาก stored procedure
        const string returnParamName = "__returnValue";
        dyn.Add(returnParamName, dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

        using var conn = Create();
        await conn.OpenAsync();

        // เรียกแบบ StoredProcedure
        var raw = await conn.QueryAsync(
            procName,
            dyn,
            commandType: CommandType.StoredProcedure,
            commandTimeout: _opts.CommandTimeoutSeconds
        );

        var rows = raw.AsList(); // เป็น List<dynamic> serialize-friendly

        // อ่านค่า return value (ถ้าโปรซีเยอร์มีใช้)
        int? returnValue = null;
        try { returnValue = dyn.Get<int?>(returnParamName); } catch { /* เผื่อไม่ได้ตั้งค่า */ }

        // หากคุณมี output params ในอนาคต สามารถอ่านจาก dyn.ParameterNames ได้เช่นกัน
        // (ตอนนี้เรารับเฉพาะ input, จึงยังไม่มี outParams)
        var result = new
        {
            rows,
            returnValue
        };

        return result;
    }








    //    public async Task<object> RunSelectAsync(RunSelectRequest req)
    //    {
    //        if (req is null) throw new ArgumentNullException(nameof(req));
    //        var sql = req.Sql?.Trim() ?? "";
    //        if (string.IsNullOrWhiteSpace(sql))
    //            throw new ArgumentException("Sql is required.", nameof(req.Sql));

    //        // Enforce: single-statement + SELECT only
    //        // - Must begin with SELECT
    //        // - Disallow ';' to reduce risk of multi-statement
    //        // (ตาม requirement “ไม่ block keywords” จึงไม่บล็อกคำเฉพาะ แต่อย่ามีหลาย statement)
    //        if (!Regex.IsMatch(sql, @"^\s*SELECT\b", RegexOptions.IgnoreCase))
    //            throw new ArgumentException("Only SELECT statements are allowed.", nameof(req.Sql));
    //        if (sql.Contains(';'))
    //            throw new ArgumentException("Multiple statements are not allowed (no ';').", nameof(req.Sql));

    //        var page = req.Page < 1 ? 1 : req.Page;
    //        var pageSize = req.PageSize < 1 ? 1 : (req.PageSize > 50 ? 50 : req.PageSize);

    //        // Ensure stable paging: if no ORDER BY, add a neutral one
    //        var hasOrderBy = Regex.IsMatch(sql, @"ORDER\s+BY", RegexOptions.IgnoreCase);
    //        if (!hasOrderBy)
    //        {
    //            sql += " ORDER BY (SELECT 1)";
    //        }

    //        var start = (page - 1) * pageSize + 1;
    //        var end = page * pageSize;

    //        string pagedSql = $@"
    //WITH base AS (
    //    {sql}
    //)
    //SELECT *
    //FROM (
    //    SELECT b.*, ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS rn
    //    FROM base b
    //) x
    //WHERE x.rn BETWEEN @__start AND @__end
    //ORDER BY x.rn;";

    //        using var conn = Create();
    //        await conn.OpenAsync();

    //        var dyn = new DynamicParameters();
    //        dyn.Add("@__start", start, DbType.Int32);
    //        dyn.Add("@__end", end, DbType.Int32);

    //        // Infer & bind parameters (ชื่อพารามิเตอร์ให้ส่งมาตรงกับที่ใช้ใน SQL เช่น @CustomerId)
    //        if (req.Params is not null)
    //        {
    //            foreach (var kv in req.Params)
    //            {
    //                var (dbType, value) = InferDbType(kv.Value);
    //                dyn.Add(kv.Key, value, dbType);
    //            }
    //        }

    //        var rows = (await conn.QueryAsync(pagedSql, dyn, commandTimeout: _opts.CommandTimeoutSeconds)).AsList();
    //        return new { page, pageSize, rows };
    //    }

    // ---------- 3) Describe database (tables/columns/relations) ----------
    public async Task<IEnumerable<object>> DescribeDatabaseAsync(DescribeRequest req)
    {
        // includeDefinition ถูกเก็บไว้เพื่อรองรับการขยายในอนาคต
        bool includeDefinition = req?.IncludeDefinition ?? false;

        //        var sql = @"
        //SELECT
        //    sch_t.name AS TableSchema,
        //    t.name AS TableName,
        //    ep.value AS TableDescription,
        //    c.name AS ColumnName,
        //    ty.name AS DataType,
        //    c.max_length,
        //    c.is_nullable,
        //    cep.value AS ColumnDescription,
        //    fk.name AS ForeignKeyName,
        //    sch_ref.name AS ReferencedTableSchema,
        //    t_ref.name AS ReferencedTableName,
        //    c_ref.name AS ReferencedColumnName
        //FROM sys.tables t
        //INNER JOIN sys.schemas sch_t
        //    ON t.schema_id = sch_t.schema_id
        //LEFT JOIN sys.extended_properties ep
        //    ON ep.major_id = t.object_id
        //   AND ep.minor_id = 0
        //   AND ep.class = 1
        //   AND ep.name = 'MS_Description'
        //INNER JOIN sys.columns c
        //    ON t.object_id = c.object_id
        //INNER JOIN sys.types ty
        //    ON c.user_type_id = ty.user_type_id
        //LEFT JOIN sys.extended_properties cep
        //    ON cep.major_id = c.object_id
        //   AND cep.minor_id = c.column_id
        //   AND cep.class = 1
        //   AND cep.name = 'MS_Description'
        //LEFT JOIN sys.foreign_key_columns fkc
        //    ON fkc.parent_object_id = t.object_id
        //   AND fkc.parent_column_id = c.column_id
        //LEFT JOIN sys.foreign_keys fk
        //    ON fk.object_id = fkc.constraint_object_id
        //LEFT JOIN sys.tables t_ref
        //    ON fkc.referenced_object_id = t_ref.object_id
        //LEFT JOIN sys.schemas sch_ref
        //    ON t_ref.schema_id = sch_ref.schema_id
        //LEFT JOIN sys.columns c_ref
        //    ON t_ref.object_id = c_ref.object_id
        //   AND fkc.referenced_column_id = c_ref.column_id
        //ORDER BY sch_t.name, t.name, c.column_id;";


        var sql = @"
-- ====================================================================================
-- JSON Schema Export (Very Detailed) for LLM-safe planning & validation
--   - db, version
--   - tables[]
--       - schema, name, description, row_count
--       - columns[]: name, type(rendered), base_type, max_length, precision, scale,
--                    nullable, pk, identity, computed, collation, default, description
--       - indexes[]: name, is_primary_key, is_unique, is_disabled, filter,
--                    columns[] (ตามลำดับ), includes[]
--       - foreign_keys[]: name, to_table (schema.name), mappings[{from,to}]
--       - join_hints[]: {to (schema.name), on[]}
-- หมายเหตุ:
--   * ไม่อ้างถึงตารางเสริมภายนอก (เช่น Schema_Aliases) เพื่อเลี่ยง error ถ้าไม่มี
--   * แสดงผลเป็นคอลัมน์เดียว [json] — นำไปใช้ใน C# rdr.GetString(0)
-- ====================================================================================

SET NOCOUNT ON;

WITH
tbl AS (
    SELECT 
        t.object_id,
        s.name  AS schema_name,
        t.name  AS table_name,
        CAST(ep.value AS nvarchar(max)) AS table_description
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    OUTER APPLY (
        SELECT TOP (1) ep.value
        FROM sys.extended_properties ep
        WHERE ep.major_id = t.object_id
          AND ep.minor_id = 0
          AND ep.name = 'MS_Description'
    ) ep
),
pk AS (
    SELECT ic.object_id, ic.column_id
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
),
col_desc AS (
    SELECT ep.major_id AS object_id, ep.minor_id AS column_id, CAST(ep.value AS nvarchar(max)) AS col_description
    FROM sys.extended_properties ep
    WHERE ep.name = 'MS_Description'
),
col_full AS (
    SELECT 
        c.object_id,
        c.column_id,
        c.name AS column_name,
        ty.name AS base_type,
        c.max_length,
        c.precision,
        c.scale,
        c.is_nullable,
        c.is_identity,
        c.is_computed,
        c.collation_name,
        dc.definition AS default_definition,
        CASE 
          WHEN ty.name IN ('nvarchar','nchar') 
               THEN CASE WHEN c.max_length = -1 THEN ty.name + '(max)' ELSE ty.name + '(' + CAST(c.max_length/2 AS varchar(10)) + ')' END
          WHEN ty.name IN ('varchar','char','varbinary') 
               THEN CASE WHEN c.max_length = -1 THEN ty.name + '(max)' ELSE ty.name + '(' + CAST(c.max_length AS varchar(10)) + ')' END
          WHEN ty.name IN ('decimal','numeric') 
               THEN ty.name + '(' + CAST(c.precision AS varchar(10)) + ',' + CAST(c.scale AS varchar(10)) + ')'
          WHEN ty.name IN ('datetime2','time','datetimeoffset')
               THEN ty.name + '(' + CAST(c.scale AS varchar(10)) + ')'
          ELSE ty.name
        END AS rendered_type,
        CASE WHEN pk.object_id IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(NULL AS bit) END AS is_pk,
        cd.col_description
    FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
    LEFT JOIN col_desc cd ON cd.object_id = c.object_id AND cd.column_id = c.column_id
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
),
fks AS (
    SELECT
        fk.object_id AS fk_object_id,
        fk.name      AS fk_name,
        src_t.object_id AS src_object_id,
        src_s.name   AS src_schema,
        src_t.name   AS src_table,
        src_c.column_id AS src_column_id,
        src_c.name   AS src_column,
        ref_t.object_id AS ref_object_id,
        ref_s.name   AS ref_schema,
        ref_t.name   AS ref_table,
        ref_c.column_id AS ref_column_id,
        ref_c.name   AS ref_column
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    JOIN sys.tables src_t ON src_t.object_id = fk.parent_object_id
    JOIN sys.schemas src_s ON src_s.schema_id = src_t.schema_id
    JOIN sys.columns src_c ON src_c.object_id = src_t.object_id AND src_c.column_id = fkc.parent_column_id
    JOIN sys.tables ref_t ON ref_t.object_id = fk.referenced_object_id
    JOIN sys.schemas ref_s ON ref_s.schema_id = ref_t.schema_id
    JOIN sys.columns ref_c ON ref_c.object_id = ref_t.object_id AND ref_c.column_id = fkc.referenced_column_id
),
fk_grouped AS (
    SELECT
        src_object_id,
        fk_name,
        ref_schema,
        ref_table
    FROM fks
    GROUP BY src_object_id, fk_name, ref_schema, ref_table
),
join_hints AS (
    SELECT DISTINCT
        f.src_object_id,
        f.ref_schema,
        f.ref_table
    FROM fks f
),
rowcounts AS (
    SELECT 
        p.object_id,
        SUM(CASE WHEN p.index_id IN (0,1) THEN p.row_count ELSE 0 END) AS row_count
    FROM sys.dm_db_partition_stats p
    GROUP BY p.object_id
),
idx_base AS (
    SELECT
        i.object_id,
        i.index_id,
        i.name,
        i.is_primary_key,
        i.is_unique,
        i.is_disabled,
        i.filter_definition
    FROM sys.indexes i
    WHERE i.is_hypothetical = 0
      AND i.type_desc <> 'HEAP'
),
idx_cols AS (
    SELECT
        ic.object_id,
        ic.index_id,
        ic.is_included_column,
        ic.key_ordinal,
        c.name AS column_name
    FROM sys.index_columns ic
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
),
columns_json AS (
    SELECT
        t.object_id,
        (
            SELECT
                c.column_name     AS [name],
                c.rendered_type   AS [type],
                c.base_type       AS [base_type],
                c.max_length      AS [max_length],
                c.precision       AS [precision],
                c.scale           AS [scale],
                c.is_nullable     AS [nullable],
                c.is_pk           AS [pk],
                c.is_identity     AS [identity],
                c.is_computed     AS [computed],
                c.collation_name  AS [collation],
                c.default_definition AS [default],
                c.col_description AS [description]
            FROM col_full c
            WHERE c.object_id = t.object_id
            ORDER BY c.column_id
            FOR JSON PATH
        ) AS columns_json_text
    FROM tbl t
),
indexes_json AS (
    SELECT
        t.object_id,
        (
            SELECT
                b.name            AS [name],
                b.is_primary_key  AS [is_primary_key],
                b.is_unique       AS [is_unique],
                b.is_disabled     AS [is_disabled],
                b.filter_definition AS [filter],
                (
                    SELECT ic.column_name AS [name]
                    FROM idx_cols ic
                    WHERE ic.object_id = b.object_id
                      AND ic.index_id = b.index_id
                      AND ic.is_included_column = 0
                    ORDER BY ic.key_ordinal
                    FOR JSON PATH
                ) AS [columns],
                (
                    SELECT ic.column_name AS [name]
                    FROM idx_cols ic
                    WHERE ic.object_id = b.object_id
                      AND ic.index_id = b.index_id
                      AND ic.is_included_column = 1
                    FOR JSON PATH
                ) AS [includes]
            FROM idx_base b
            WHERE b.object_id = t.object_id
            ORDER BY b.is_primary_key DESC, b.is_unique DESC, b.name
            FOR JSON PATH
        ) AS indexes_json_text
    FROM tbl t
),
foreign_keys_json AS (
    SELECT
        t.object_id,
        (
            SELECT
                g.fk_name     AS [name],
                CONCAT(g.ref_schema, '.', g.ref_table) AS [to_table],
                (
                    SELECT 
                        CONCAT(f.src_table, '.', f.src_column) AS [from],
                        CONCAT(f.ref_table, '.', f.ref_column) AS [to]
                    FROM fks f
                    WHERE f.src_object_id = t.object_id
                      AND f.fk_name = g.fk_name
                    FOR JSON PATH
                ) AS [mappings]
            FROM fk_grouped g
            WHERE g.src_object_id = t.object_id
            ORDER BY g.fk_name
            FOR JSON PATH
        ) AS foreign_keys_json_text
    FROM tbl t
),
join_hints_json AS (
    SELECT
        t.object_id,
        (
            SELECT 
                CONCAT(j.ref_schema, '.', j.ref_table) AS [to],
                (
                    SELECT 
                        CONCAT(f.src_table, '.', f.src_column, ' = ', f.ref_table, '.', f.ref_column) AS [on]
                    FROM fks f
                    WHERE f.src_object_id = t.object_id
                      AND f.ref_table = j.ref_table
                      AND f.ref_schema = j.ref_schema
                    FOR JSON PATH
                ) AS [on]
            FROM join_hints j
            WHERE j.src_object_id = t.object_id
            GROUP BY j.ref_schema, j.ref_table
            FOR JSON PATH
        ) AS join_hints_json_text
    FROM tbl t
)

SELECT [json] =
(
    SELECT
        DB_NAME() AS [db],
        CONVERT(nvarchar(30), SYSUTCDATETIME(), 127) AS [version],
        (
            SELECT
                t.schema_name AS [schema],
                t.table_name  AS [name],
                t.table_description AS [description],
                ISNULL(rc.row_count, 0) AS [row_count],

                JSON_QUERY(cj.columns_json_text)      AS [columns],
                JSON_QUERY(ij.indexes_json_text)      AS [indexes],
                JSON_QUERY(fj.foreign_keys_json_text) AS [foreign_keys],
                CASE 
                    WHEN jh.join_hints_json_text IS NOT NULL 
                    THEN JSON_QUERY(jh.join_hints_json_text)
                    ELSE JSON_QUERY('[]')
                END AS [join_hints]

            FROM tbl t
            LEFT JOIN columns_json cj       ON cj.object_id = t.object_id
            LEFT JOIN indexes_json ij       ON ij.object_id = t.object_id
            LEFT JOIN foreign_keys_json fj  ON fj.object_id = t.object_id
            LEFT JOIN join_hints_json jh    ON jh.object_id = t.object_id
            LEFT JOIN rowcounts rc          ON rc.object_id = t.object_id
            ORDER BY t.schema_name, t.table_name
            FOR JSON PATH
        ) AS [tables]
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
);



    ";







        using var conn = Create();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync(sql, commandTimeout: _opts.CommandTimeoutSeconds);
        return rows;
    }

    // ---------- 4) List stored procedures ----------
    public async Task<IEnumerable<object>> ListProceduresAsync(DescribeRequest req)
    {
        bool includeDefinition = req?.IncludeDefinition ?? false;

        var sql = @"
SELECT 
    s.name AS SchemaName,
    p.name AS ProcedureName,
    ep.value AS ProcedureDescription,
    (
        SELECT 
            pa.parameter_id   AS ParameterId,
            pa.name           AS Name,
            TYPE_NAME(pa.user_type_id) AS SqlType,
            CASE 
                WHEN TYPE_NAME(pa.user_type_id) IN (N'nchar', N'nvarchar', N'char', N'varchar', N'binary', N'varbinary')
                    THEN CASE 
                            WHEN pa.max_length = -1 THEN N'MAX'
                            WHEN TYPE_NAME(pa.user_type_id) LIKE N'n%' THEN CAST(pa.max_length/2 AS nvarchar(10))
                            ELSE CAST(pa.max_length AS nvarchar(10)) 
                         END
                ELSE NULL
            END               AS LengthOrMax,
            CASE WHEN TYPE_NAME(pa.user_type_id) IN (N'decimal', N'numeric') THEN pa.[precision] END AS [Precision],
            CASE WHEN TYPE_NAME(pa.user_type_id) IN (N'decimal', N'numeric') THEN pa.scale END AS [Scale],
            pa.is_output      AS IsOutput,
            CASE WHEN pa.is_output = 1 THEN N'OUTPUT' ELSE N'INPUT' END AS [Direction],
            pa.is_nullable    AS IsNullable,
            -- DefaultValue: พาร์สจากส่วน header ของโปรซีเยอร์
            LTRIM(RTRIM(dv.DefaultValue)) AS DefaultValue,
            CAST(ep2.value AS nvarchar(max)) AS Description
        FROM sys.parameters pa
        LEFT JOIN sys.extended_properties ep2
          ON ep2.class = 2
         AND ep2.major_id = pa.object_id
         AND ep2.minor_id = pa.parameter_id
         AND ep2.name = N'MS_Description'
        CROSS APPLY (
            -- กำหนดขอบเขต header = ช่วงตั้งแต่คำว่า 'PROCEDURE' ถึงหน้าคำว่า 'AS'
            SELECT 
                def     = m.definition,
                procPos = NULLIF(CHARINDEX('PROCEDURE', m.definition), 0),
                asPos   = NULLIF(CHARINDEX('AS',        m.definition), 0)
        ) hdr
        CROSS APPLY (
            SELECT header = 
                CASE 
                    WHEN hdr.procPos IS NOT NULL AND hdr.asPos IS NOT NULL AND hdr.asPos > hdr.procPos
                        THEN SUBSTRING(hdr.def, hdr.procPos, hdr.asPos - hdr.procPos)
                    ELSE hdr.def -- ถ้าหาไม่เจอ ใช้ทั้ง definition เผื่อกรณีพิเศษ
                END
        ) h
        CROSS APPLY (
            -- หา '=' หลังชื่อพารามิเตอร์ภายใน header
            SELECT 
                namePos = NULLIF(CHARINDEX(pa.name, h.header), 0),
                eqPos   = CASE 
                            WHEN CHARINDEX(pa.name, h.header) > 0 
                                THEN NULLIF(CHARINDEX('=', h.header, CHARINDEX(pa.name, h.header)), 0)
                            ELSE NULL
                          END
        ) pos
        CROSS APPLY (
            -- หาตำแหน่งสิ้นสุด default: คอมมาหรือวงเล็บปิดที่อยู่ถัดไป ตัดตอนที่ใกล้ที่สุด
            SELECT 
                commaPos = CASE WHEN pos.eqPos IS NULL THEN 0 ELSE CHARINDEX(',', h.header, pos.eqPos + 1) END,
                parenPos = CASE WHEN pos.eqPos IS NULL THEN 0 ELSE CHARINDEX(')', h.header, pos.eqPos + 1) END
        ) endpos
        CROSS APPLY (
            SELECT 
                endAt = CASE 
                            WHEN pos.eqPos IS NULL THEN 0
                            ELSE 
                                CASE 
                                    WHEN (CASE WHEN endpos.commaPos = 0 THEN 2147483647 ELSE endpos.commaPos END) 
                                         <  (CASE WHEN endpos.parenPos = 0 THEN 2147483647 ELSE endpos.parenPos END)
                                        THEN (CASE WHEN endpos.commaPos = 0 THEN LEN(h.header) + 1 ELSE endpos.commaPos END)
                                    ELSE (CASE WHEN endpos.parenPos = 0 THEN LEN(h.header) + 1 ELSE endpos.parenPos END)
                                END
                        END
        ) cut
        CROSS APPLY (
            SELECT 
                DefaultValue = CASE 
                                  WHEN pos.eqPos IS NULL OR cut.endAt = 0 OR cut.endAt <= pos.eqPos 
                                      THEN NULL
                                  ELSE SUBSTRING(h.header, pos.eqPos + 1, cut.endAt - (pos.eqPos + 1))
                               END
        ) dv
        WHERE pa.object_id = p.object_id
        ORDER BY pa.parameter_id
        FOR JSON PATH
    ) AS ParamsJson
FROM sys.procedures p
JOIN sys.schemas s
  ON p.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep
  ON ep.major_id = p.object_id
 AND ep.minor_id = 0
 AND ep.class = 1
 AND ep.name = N'MS_Description'
LEFT JOIN sys.sql_modules m
  ON p.object_id = m.object_id
ORDER BY s.name, p.name;

";

        using var conn = Create();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync(sql, commandTimeout: _opts.CommandTimeoutSeconds);
        return rows;
    }

    // ---------- Type inference helper ----------
    private static (DbType? dbType, object? value) InferDbType(object? val)
    {
        if (val is null) return (DbType.Object, DBNull.Value);

        switch (val)
        {
            case int or short or byte: return (DbType.Int32, Convert.ToInt32(val));
            case long: return (DbType.Int64, val);
            case bool: return (DbType.Boolean, val);
            case decimal: return (DbType.Decimal, val);
            case double: return (DbType.Double, val);
            case float: return (DbType.Single, val);
            case DateTime: return (DbType.DateTime2, val);
            case DateTimeOffset: return (DbType.DateTimeOffset, val);
            case Guid: return (DbType.Guid, val);
            case string s:
                // ลอง parse ตามลำดับ DateTimeOffset -> DateTime -> long -> decimal
                if (DateTimeOffset.TryParse(s, out var dto)) return (DbType.DateTimeOffset, dto);
                if (DateTime.TryParse(s, out var dt)) return (DbType.DateTime2, dt);
                if (long.TryParse(s, out var l)) return (DbType.Int64, l);
                if (decimal.TryParse(s, out var d)) return (DbType.Decimal, d);
                return (DbType.String, s);
            default:
                return (DbType.Object, val);
        }
    }
}

// ===== Requests (ควรอยู่ที่ไฟล์สาธารณะของโปรเจกต์ คุณมีอยู่แล้ว แต่ใส่ไว้ให้ครบในที่เดียว) =====
//public sealed class RunSelectRequest
//{
//    public string Sql { get; set; } = "";
//    public Dictionary<string, object>? Params { get; set; }
//    public int Page { get; set; } = 1;
//    public int PageSize { get; set; } = 50;
//}

//public sealed class DescribeRequest
//{
//    public bool? IncludeDefinition { get; set; }
//}
