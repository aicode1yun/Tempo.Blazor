# Fáze 9 — Kompletní vzorce a výpočty

> Stav: ☐ Neza­počato · Závisí na: Fáze 1, 6 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Dotáhnout výpočetní jádro k Excel/OnlyOffice paritě:
- **Knihovna funkcí** z ~80 na cílových ~480 (po kategoriích, každá funkce TDD).
- **Průvodce funkcí** (FormulaWizard) + vylepšený našeptávač argumentů.
- **Watch Window** (sledování buněk).
- **Iterativní výpočet** (povolení cyklických odkazů s limitem iterací/přesnosti).
- **Dynamická pole / spill** (funkce vracející pole se „rozlijí").

OnlyOffice reference: `view/FormulaWizard.js`, `view/FormulaDialog.js`, `view/FormulaTab.js`, `view/WatchDialog.js`, `model/FormulaObjects/*`. Naše jádro: `Abstractions/Spreadsheet/Formula/*`, `Functions/SpreadsheetFunctions.cs`, `FunctionRegistry`, `SpreadsheetFormulaFunctionCatalog`.

> **Pozn.:** Tato fáze je nejlépe inkrementální — kategorie funkcí se dají dělat průběžně. Doporučeno rozdělit do pod-iterací 9.x dle priority (math/stat/lookup nejdřív).

---

## ČÁST A — Infrastruktura jádra (předpoklad pro nové funkce)

### 9A.1 Typový a chybový systém
- [ ] **(test)** `FormulaErrorTests`: kompletní sada chyb `#DIV/0!, #VALUE!, #REF!, #NAME?, #NUM!, #N/A, #NULL!, #SPILL!, #CALC!` + propagace skrz operace.
- [ ] Doplnit chybějící chyby do `FormulaError` + propagaci ve `FormulaEvaluator`.
- [ ] **(test)** zelené.

### 9A.2 Koerce a porovnávání
- [ ] **(test)** `CoercionTests`: text↔číslo↔bool↔datum dle Excel pravidel; prázdná buňka jako 0/"".
- [ ] **(test)** `ComparisonTests`: textové porovnání case-insensitive, čísla, řazení typů.
- [ ] Sjednotit koerci v evaluatoru.
- [ ] **(test)** zelené.

### 9A.3 Argumenty: pole, rozsahy, reference
- [ ] **(test)** funkce přijímají skalár / rozsah / pole / referenci; oštření „array argument" (np. `SUMPRODUCT`).
- [ ] Rozšířit dispatcher funkcí o jednotné předání argumentů (skalár vs. range vs. array).
- [ ] **(test)** zelené.

---

## ČÁST B — Knihovna funkcí po kategoriích (každá funkce = TDD)

> Konvence (viz master §6): implementace v `SpreadsheetFunctions.cs` + registrace v `FunctionRegistry` + nápověda v `SpreadsheetFormulaFunctionCatalog.cs` (název, popis, signatura argumentů — **lokalizováno**) + test v `SpreadsheetFunction<Kategorie>Tests.cs`. U každé funkce: happy path + okrajové případy + chybové stavy.

### 9B.1 Matematické a trig.
- [ ] SUMIF, SUMIFS, SUMPRODUCT, SUBTOTAL, AGGREGATE, ROUND* (už část), MROUND, CEILING(.MATH/.PRECISE), FLOOR(.MATH/.PRECISE), INT, TRUNC, SIGN, GCD, LCM, FACT, FACTDOUBLE, COMBIN(A), PERMUT(ATIONA), QUOTIENT, PRODUCT, POWER (✓), EXP, LN, LOG, LOG10, SQRT(✓), SQRTPI, ABS(✓), MOD(✓), RAND(✓)/RANDBETWEEN(✓)/RANDARRAY, SIN/COS/TAN/ASIN/ACOS/ATAN/ATAN2/SINH/COSH/TANH/ASINH/ACOSH/ATANH, DEGREES, RADIANS, PI(✓), ROMAN, ARABIC, BASE, DECIMAL, SUMSQ, SERIESSUM, MULTINOMIAL.
- [ ] (test per funkce) zelené.

### 9B.2 Statistické
- [ ] AVERAGE(✓)/AVERAGEA/AVERAGEIF/AVERAGEIFS, COUNT(✓)/COUNTA/COUNTBLANK/COUNTIF(✓?)/COUNTIFS, MAX(✓)/MAXA/MAXIFS, MIN(✓)/MINA/MINIFS, MEDIAN, MODE(.SNGL/.MULT), STDEV(.S/.P)/STDEVA/STDEVPA, VAR(.S/.P)/VARA/VARPA, LARGE, SMALL, RANK(.EQ/.AVG), PERCENTILE(.INC/.EXC), QUARTILE(.INC/.EXC), PERCENTRANK, FREQUENCY, CORREL, COVARIANCE(.S/.P), SLOPE, INTERCEPT, FORECAST(.LINEAR), TREND, GROWTH, LINEST, LOGEST, NORM(.DIST/.INV/.S.DIST/.S.INV), STANDARDIZE, CONFIDENCE(.NORM/.T), BINOM.DIST, POISSON.DIST, EXPON.DIST, T.DIST*/T.INV*, CHISQ.*, F.*, BETA.*, GAMMA*, WEIBULL.DIST, GEOMEAN, HARMEAN, TRIMMEAN, DEVSQ, AVEDEV, SKEW(.P), KURT, PROB, COUNTIF/COUNTIFS okrajové.
- [ ] (test per funkce) zelené.

### 9B.3 Vyhledávací a referenční
- [ ] VLOOKUP(✓)/HLOOKUP(✓), XLOOKUP, LOOKUP, INDEX(✓)/MATCH(✓)/XMATCH, OFFSET(✓), INDIRECT(✓), CHOOSE(✓), ROW(✓)/ROWS(✓)/COLUMN(✓)/COLUMNS(✓), AREAS(✓), ADDRESS(✓), FORMULATEXT, GETPIVOTDATA (návaznost na Fázi 13), HYPERLINK, TRANSPOSE, SORT, SORTBY, FILTER, UNIQUE, SEQUENCE (návaznost na dynamická pole §D).
- [ ] (test per funkce) zelené.

### 9B.4 Textové
- [ ] LEFT(✓)/RIGHT(✓)/MID(✓), LEN(✓), FIND(✓)/SEARCH(✓), SUBSTITUTE(✓)/REPLACE, CONCAT(ENATE✓)/TEXTJOIN, TEXT(✓)/VALUE(✓)/NUMBERVALUE, UPPER(✓)/LOWER(✓)/PROPER(✓), TRIM(✓)/CLEAN, REPT(✓), CHAR/UNICHAR/CODE/UNICODE, EXACT, DOLLAR, FIXED, T, TEXTBEFORE/TEXTAFTER/TEXTSPLIT, LEFTB/RIGHTB/MIDB/LENB (volitelné), ASC/JIS (volitelné).
- [ ] (test per funkce) zelené.

### 9B.5 Datum a čas
- [ ] DATE(✓)/TIME(✓), DATEVALUE(✓)/TIMEVALUE(✓), YEAR(✓)/MONTH(✓)/DAY/HOUR(✓)/MINUTE(✓)/SECOND(✓), TODAY(✓)/NOW(✓), WEEKDAY(✓)/WEEKNUM(✓)/ISOWEEKNUM, EDATE(✓)/EOMONTH(✓), DATEDIF(✓), DAYS(✓)/DAYS360, NETWORKDAYS(.INTL)/WORKDAY(.INTL), YEARFRAC, DAY (chybí?), MONTH okrajové.
- [ ] (test per funkce) zelené.

### 9B.6 Logické a informační
- [ ] IF(✓)/IFS/IFERROR(✓)/IFNA, AND(✓)/OR(✓)/NOT(✓)/XOR, TRUE(✓)/FALSE(✓), SWITCH, ISBLANK(✓)/ISERROR(✓)/ISERR/ISNA/ISNUMBER(✓)/ISTEXT(✓)/ISNONTEXT/ISLOGICAL(✓)/ISREF/ISFORMULA/ISEVEN(✓)/ISODD(✓), N, NA, TYPE, ERROR.TYPE, ISOMITTED (lambda), CELL, INFO, SHEET/SHEETS.
- [ ] (test per funkce) zelené.

### 9B.7 Finanční
- [ ] PMT, PPMT, IPMT, FV, PV, NPV, XNPV, IRR, XIRR, MIRR, RATE, NPER, PRICE, YIELD, DURATION, SLN, SYD, DB, DDB, VDB, EFFECT, NOMINAL, CUMIPMT, CUMPRINC, ACCRINT, COUPNUM, INTRATE, RECEIVED, DISC, DOLLARDE/DOLLARFR.
- [ ] (test per funkce) zelené.

### 9B.8 Inženýrské / databázové / web (dle priority)
- [ ] Engineering: CONVERT, HEX2*/BIN2*/OCT2*/DEC2*, BITAND/BITOR/BITXOR/BITLSHIFT/BITRSHIFT, COMPLEX/IM*, DELTA, GESTEP, ERF/ERFC, BESSEL*.
- [ ] Database: DSUM, DAVERAGE, DCOUNT(A), DMAX, DMIN, DGET, DPRODUCT, DSTDEV(P), DVAR(P).
- [ ] Web (volitelné, dle bezpečnosti hostitele): WEBSERVICE, FILTERXML, ENCODEURL — **jen přes abstrakci hostitele**, ne přímý síťový přístup z komponenty.
- [ ] (test per funkce) zelené.

### 9B.9 Pokrytí & katalog
- [ ] **(test)** `FunctionRegistryCoverageTests`: každá registrovaná funkce má katalogovou nápovědu (lokalizovaný popis + argumenty) a alespoň jeden test.
- [ ] Lokalizovat názvy/popisy argumentů do 3 resx (`TmSpreadsheet_Fn_<NAME>_Desc`, `_Arg<i>`).

---

## ČÁST C — Průvodce & našeptávač

### 9C.1 Formula Wizard
- [ ] **(bUnit, failing)** `TmSpreadsheetFormulaWizardDialog`: vyhledání funkce dle kategorie/názvu, popis, pole pro argumenty s živým náhledem výsledku a referencemi (lze vybírat rozsah v gridu), vložení do buňky.
- [ ] Vytvořit dialog + lokalizace `TmSpreadsheet_FormulaWizard_*`.
- [ ] **(bUnit)** zelené.

### 9C.2 Vylepšený našeptávač v řádku vzorců
- [ ] **(test/bUnit)** rozšířit existující nápovědu (`SpreadsheetFormulaFunctionHint`) o: tooltip se signaturou a zvýrazněním aktuálního argumentu, dokončování názvů funkcí i pojmenovaných rozsahů, kategorie.
- [ ] **(E2E)** psaní `=SU` nabídne SUM/SUMIF/…; po `(` se ukáže signatura.

---

## ČÁST D — Dynamická pole (spill)

### 9D.1 Engine
- [ ] **(test)** `SpillTests`: funkce vracející pole (`SEQUENCE`, `FILTER`, `SORT`, `UNIQUE`, `TRANSPOSE`) se „rozlijí" do sousedních buněk; `#SPILL!` při kolizi s neprázdnou buňkou.
- [ ] Rozšířit model: buňka může být „spill anchor" + „spill range"; čtení rozlitých buněk je read-only odraz.
- [ ] **(test)** zelené (vč. přepočtu při změně velikosti pole).

### 9D.2 Rendering
- [ ] JS canvas: vizuálně označit spill rozsah (jemné ohraničení) při výběru kotvy.
- [ ] **(E2E)** `=SEQUENCE(3)` rozlije 3 buňky; vyplnění sousední buňky → `#SPILL!`.

---

## ČÁST E — Iterativní výpočet

### 9E.1 Volby výpočtu
- [ ] **(test)** `IterativeCalcTests`: při povolení (max iterací, max změna) cyklické odkazy konvergují místo chyby; při zakázání zůstává detekce cyklu (dnešní chování).
- [ ] Přidat `SpreadsheetWorkbook.Calculation { bool Iterative, int MaxIterations=100, double MaxChange=0.001, CalcMode(Auto|Manual) }`.
- [ ] Rozšířit přepočtový engine o iterace + ruční přepočet (`F9`).
- [ ] **(test)** zelené.

### 9E.2 UI
- [ ] **(bUnit)** dialog/volby výpočtu (záložka Vzorce): automaticky/ručně, iterativní výpočet + meze, „Přepočítat" (`F9`).
- [ ] Lokalizace `TmSpreadsheet_Calc_*`.

---

## ČÁST F — Watch Window

### 9F.1 Model + UI
- [ ] **(test)** `WatchListTests`: přidání/odebrání sledované buňky; položka drží sešit/list/buňku/název/hodnotu/vzorec, aktualizuje se při přepočtu.
- [ ] **(bUnit)** `TmSpreadsheetWatchPanel`: tabulka sledovaných buněk; přidat výběrem, odebrat; dvojklik skočí na buňku.
- [ ] Vytvořit panel + lokalizace `TmSpreadsheet_Watch_*`.
- [ ] **(E2E)** přidat buňku do watch → změna zdroje → hodnota ve watch se aktualizuje.

---

## ČÁST G — Screenshot + XLSX
- [ ] Baseline `formula-01-wizard.png`, `formula-02-autocomplete.png`, `formula-03-spill.png`, `formula-04-watch.png` + UX sign-off.
- [ ] **(test)** XLSX round-trip nově podporovaných funkcí a spill (`<f t="array"/>`), iterativních voleb (`calcPr`).

---

## Definition of Done (Fáze 9)
- [ ] Cílové pokrytí funkcí (priority math/stat/lookup/text/date/logical/financial hotové; každá s testem a lokalizovaným katalogem).
- [ ] Formula Wizard + vylepšený našeptávač (signatura, named ranges).
- [ ] Dynamická pole (spill) vč. `#SPILL!`; iterativní výpočet + ruční přepočet; Watch Window.
- [ ] Kompletní chybový systém a koerce dle Excelu.
- [ ] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 9 na ✅.
