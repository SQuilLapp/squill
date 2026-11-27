select 1 from dual
-- ---
select sysdate, systimestamp from dual
-- ---
select nvl(null, 'x'), nvl2(1, 'a', 'b'), decode(1, 1, 'one', 'other') from dual
-- ---
select to_char(sysdate, 'YYYY-MM-DD'), to_date('2026-11-04', 'YYYY-MM-DD') from dual
-- ---
select q'[it's]' as s, q'!a;b!' as t, n'x' as u from dual
-- ---
-- unsupported: CONNECT BY is not parsed yet
select level from dual connect by level <= 5
-- ---
with t as (select 1 n from dual union all select 2 from dual) select sum(n) from t
-- ---
select listagg(n, ',') within group (order by n) from (select 1 n from dual union all select 2 from dual)
-- ---
select * from (select 1 n from dual union all select 2 from dual union all select 3 from dual) order by n desc fetch first 3 rows only
-- ---
select * from (select 1 n from dual union all select 2 from dual union all select 3 from dual) offset 2 rows fetch next 3 rows only
-- ---
select rownum from dual
-- ---
select cast(1 as number(10, 2)), cast('a' as varchar2(10)) from dual
-- ---
select case when 1 = 1 then 'y' else 'n' end from dual
-- ---
select a.n, b.n from (select 1 n from dual) a left join (select 2 n from dual) b on a.n = b.n
-- ---
select 1 from dual union all select 2 from dual
-- ---
select 'a' || 'b', length('abc'), substr('abc', 2), instr('abc', 'b') from dual
-- ---
select extract(year from sysdate), trunc(sysdate), add_months(sysdate, 1) from dual
-- ---
select 1 from dual;
-- ---
begin
  null;
end;
/
-- ---
declare
  v number;
begin
  select 1 into v from dual;
  if v = 1 then
    null;
  end if;
end;
/
-- ---
-- statements: 2
select 1 from dual;
select 2 from dual;
-- ---
-- statements: 2
begin
  null;
end;
/
select 1 from dual
/
