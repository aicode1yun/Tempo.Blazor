# Email template syntax (Scriban)

Tempo.Blazor email templates use [Scriban](https://github.com/scriban/scriban) for variable
substitution. Expressions live in `{{ … }}`; everything else is emitted verbatim. The engine runs in
a **sandbox** (`ScribanTemplateEngine` + `TemplateSecurityOptions`): `include` is disabled, .NET
reflection is not exposed, and loops/recursion/output/time are bounded.

> Variable access is **snake_case**. A C# property `FirstName` (or a JSON field `firstName`) is
> referenced as `{{ first_name }}`.

---

## Variables

```
{{ name }}
{{ user.email }}
{{ order.customer.name }}
{{ items[0].product_name }}
```

Missing variables render as empty text by default (lenient mode). With
`TemplateSecurityOptions.StrictVariables = true`, referencing an undefined variable is an error.

---

## Filters

Filters transform values with the pipe `|` operator:

```
{{ name | string.upcase }}
{{ name | string.downcase }}
{{ name | string.capitalize }}
{{ name | string.truncate 50 }}
{{ name | object.default "Guest" }}
{{ text | string.replace "old" "new" }}
{{ csv  | string.split "," }}
{{ text | string.strip }}
{{ items | array.join ", " }}
{{ items.size }}
{{ created | date.to_string "%Y-%m-%d" }}
{{ price | math.format "N2" }}
```

The full Scriban built-in function set is available under `string`, `array`, `object`, `math`,
`date`, `regex`, `html` and `timespan`. None of these touch the file system or network.

---

## Conditions

```
{{ if status == "active" }}
  Active
{{ else if status == "pending" }}
  Pending
{{ else }}
  Inactive
{{ end }}
```

Comparison: `==` `!=` `<` `>` `<=` `>=` — Logical: `&&` `||` `!`

---

## Loops

```
{{ for item in items }}
  {{ for.index }}: {{ item.name }}
{{ end }}
```

Loop helpers: `for.index` (0-based), `for.first`, `for.last`, `for.even`, `for.odd`.

> The loop variable (`item` above) is local to the loop and is **not** treated as a template
> variable. The iterated collection (`items`) is detected as a collection-typed variable.

---

## Block visibility (Tempo extension)

Any block can carry a `VisibleWhen` expression (a bare Scriban boolean, no braces). The generator
wraps the block in `{{ if <expr> }} … {{ end }}`, so it only renders when the expression is truthy.

---

## Sandbox limits (`TemplateSecurityOptions`)

| Option | Default | Purpose |
|---|---|---|
| `LoopLimit` | 5000 | Aborts runaway / infinite loops |
| `RecursiveLimit` | 100 | Aborts infinite recursion |
| `MaxOutputLength` | 5,000,000 | Caps rendered output size |
| `Timeout` | 5 s | Cooperative wall-clock limit |
| `StrictVariables` | false | Undefined variable → error when true |

`include` is always disabled (no template loader is configured) and no .NET types/reflection are
reachable from a template.
