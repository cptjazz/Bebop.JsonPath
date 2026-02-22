---
title: "RFC 9535 JSONPath – Comprehensive Examples & Test Cases"
description: >
  Collected examples and test cases from RFC 9535 organized by feature area.
  Use as a conformance reference when implementing and testing a JSONPath engine.
source: "https://www.rfc-editor.org/rfc/rfc9535"
globs:
  - "**/*.cs"
  - "**/*Test*.cs"
  - "**/*test*.json"
---

# RFC 9535 – Comprehensive Examples & Test Cases

All examples below are drawn from or derived from RFC 9535. They are organized
by feature for easy use as conformance tests.

---

## 1 Root Identifier

**JSON:**
```json
{"k": "v"}
```

| Query | Result | Normalized Path |
|---|---|---|
| `$` | `{"k": "v"}` | `$` |

---

## 2 Name Selector

**JSON:**
```json
{
  "o": {"j j": {"k.k": 3}},
  "'": {"@": 2}
}
```

| Query | Result | Normalized Paths | Comment |
|---|---|---|---|
| `$.o['j j']` | `{"k.k": 3}` | `$['o']['j j']` | Named value in nested object |
| `$.o['j j']['k.k']` | `3` | `$['o']['j j']['k.k']` | Nesting further down |
| `$.o["j j"]["k.k"]` | `3` | `$['o']['j j']['k.k']` | Double-quoted (same result) |
| `$["'"]["@"]` | `2` | `$['\'']['@']` | Unusual member names |

---

## 3 Wildcard Selector

**JSON:**
```json
{
  "o": {"j": 1, "k": 2},
  "a": [5, 3]
}
```

| Query | Result | Comment |
|---|---|---|
| `$[*]` | `{"j":1,"k":2}`, `[5,3]` | Object values |
| `$.o[*]` | `1`, `2` (order may vary) | Object member values |
| `$.o[*, *]` | four values, non-deterministic | Duplicated wildcard |
| `$.a[*]` | `5`, `3` | Array elements in order |

---

## 4 Index Selector

**JSON:**
```json
["a", "b"]
```

| Query | Result | Normalized Path | Comment |
|---|---|---|---|
| `$[1]` | `"b"` | `$[1]` | Positive index |
| `$[-2]` | `"a"` | `$[0]` | Negative index |

---

## 5 Array Slice Selector

**JSON:**
```json
["a", "b", "c", "d", "e", "f", "g"]
```

| Query | Result | Comment |
|---|---|---|
| `$[1:3]` | `"b"`, `"c"` | Default step |
| `$[5:]` | `"f"`, `"g"` | No end index |
| `$[1:5:2]` | `"b"`, `"d"` | Step 2 |
| `$[5:1:-2]` | `"f"`, `"d"` | Negative step |
| `$[::-1]` | `"g"`,`"f"`,`"e"`,`"d"`,`"c"`,`"b"`,`"a"` | Reverse |

---

## 6 Filter Selector — Comparisons

**JSON:**
```json
{
  "obj": {"x": "y"},
  "arr": [2, 3]
}
```

| Comparison | Result | Comment |
|---|---|---|
| `$.absent1 == $.absent2` | true | Both empty |
| `$.absent1 <= $.absent2` | true | == implies <= |
| `$.absent == 'g'` | false | Empty vs value |
| `$.absent1 != $.absent2` | false | Both empty |
| `$.absent != 'g'` | true | Empty vs value |
| `1 <= 2` | true | Numeric |
| `1 > 2` | false | Numeric |
| `13 == '13'` | false | Type mismatch |
| `'a' <= 'b'` | true | String comparison |
| `'a' > 'b'` | false | String comparison |
| `$.obj == $.arr` | false | Type mismatch |
| `$.obj != $.arr` | true | Type mismatch |
| `$.obj == $.obj` | true | Object equality |
| `$.arr == $.arr` | true | Array equality |
| `$.obj == 17` | false | Type mismatch |
| `$.obj <= $.arr` | false | No < for objects/arrays |
| `1 <= $.arr` | false | No < for arrays |
| `true <= true` | true | == implies <= |
| `true > true` | false | No < for booleans |

---

## 7 Filter Selector — Queries

**JSON:**
```json
{
  "a": [3, 5, 1, 2, 4, 6,
        {"b": "j"},
        {"b": "k"},
        {"b": {}},
        {"b": "kilo"}],
  "o": {"p": 1, "q": 2, "r": 3, "s": 5, "t": {"u": 6}},
  "e": "f"
}
```

| Query | Result | Comment |
|---|---|---|
| `$.a[?@.b == 'kilo']` | `{"b":"kilo"}` | Member value comparison |
| `$.a[?(@.b == 'kilo')]` | `{"b":"kilo"}` | Parenthesized (equivalent) |
| `$.a[?@>3.5]` | `5`, `4`, `6` | Numeric comparison |
| `$.a[?@.b]` | `{"b":"j"}`,`{"b":"k"}`,`{"b":{}}`,`{"b":"kilo"}` | Existence test |
| `$[?@.*]` | `[3,5,…]`, `{"p":1,…}` | Non-singular existence |
| `$[?@[?@.b]]` | `[3,5,…]` | Nested filters |
| `$.o[?@<3, ?@<3]` | `1`,`2`,`2`,`1` (non-deterministic) | Multiple filter selectors |
| `$.a[?@<2 \|\| @.b == "k"]` | `1`, `{"b":"k"}` | Logical OR |
| `$.a[?match(@.b, "[jk]")]` | `{"b":"j"}`, `{"b":"k"}` | Regex full match |
| `$.a[?search(@.b, "[jk]")]` | `{"b":"j"}`,`{"b":"k"}`,`{"b":"kilo"}` | Regex substring match |
| `$.o[?@>1 && @<4]` | `2`, `3` (order may vary) | Logical AND |
| `$.o[?@.u \|\| @.x]` | `{"u":6}` | Existence OR |
| `$.a[?@.b == $.x]` | `3`,`5`,`1`,`2`,`4`,`6` | Both sides empty → equal |
| `$.a[?@ == @]` | all 10 elements | Self-equality (primitives & structured) |

---

## 8 Child Segment

**JSON:**
```json
["a", "b", "c", "d", "e", "f", "g"]
```

| Query | Result | Comment |
|---|---|---|
| `$[0, 3]` | `"a"`, `"d"` | Multiple indices |
| `$[0:2, 5]` | `"a"`, `"b"`, `"f"` | Slice + index |
| `$[0, 0]` | `"a"`, `"a"` | Duplicates preserved |

---

## 9 Descendant Segment

**JSON:**
```json
{
  "o": {"j": 1, "k": 2},
  "a": [5, 3, [{"j": 4}, {"k": 6}]]
}
```

| Query | Result | Comment |
|---|---|---|
| `$..j` | `1`, `4` (order may vary) | Recursive name lookup |
| `$..[0]` | `5`, `{"j":4}` | Index at all depths |
| `$..[*]` / `$..*` | all values recursively | Full descent |
| `$..o` | `{"j":1,"k":2}` | Input node is visited |
| `$.o..[*, *]` | `1`,`2`,`2`,`1` | Non-deterministic |
| `$.a..[0, 1]` | `5`,`3`,`{"j":4}`,`{"k":6}` | Per-visited-node application |

---

## 10 Null Semantics

**JSON:**
```json
{"a": null, "b": [null], "c": [{}], "null": 1}
```

| Query | Result | Comment |
|---|---|---|
| `$.a` | `null` | Object value |
| `$.a[0]` | *(empty)* | null is not an array |
| `$.a.d` | *(empty)* | null is not an object |
| `$.b[0]` | `null` | Array value |
| `$.b[*]` | `null` | Wildcard on array |
| `$.b[?@]` | `null` | Existence: node exists |
| `$.b[?@==null]` | `null` | Value equals null |
| `$.c[?@.d==null]` | *(empty)* | Missing ≠ null |
| `$.null` | `1` | Member name "null" |

---

## 11 Normalized Paths

| Input Path | Normalized Path | Comment |
|---|---|---|
| `$.a` | `$['a']` | Dot notation → bracket |
| `$[1]` | `$[1]` | Already normalized |
| `$[-3]` (len=5) | `$[2]` | Negative → non-negative |
| `$.a.b[1:2]` | `$['a']['b'][1]` | Nested |
| `$["\u000B"]` | `$['\u000b']` | Non-printable → hex escape |
| `$["\u0061"]` | `$['a']` | Printable → unescaped |

---

## 12 Function Extension Well-Typedness

| Query | Well-Typed? | Reason |
|---|---|---|
| `$[?length(@) < 3]` | ✅ | Singular query → ValueType |
| `$[?length(@.*) < 3]` | ❌ | `@.*` non-singular → not ValueType |
| `$[?count(@.*) == 1]` | ✅ | Query → NodesType |
| `$[?count(1) == 1]` | ❌ | Literal is not NodesType |
| `$[?match(@.timezone, 'Europe/.*')]` | ✅ | Both args ValueType |
| `$[?match(@.timezone, 'Europe/.*') == true]` | ❌ | LogicalType not comparable |
| `$[?value(@..color) == "red"]` | ✅ | value() returns ValueType |
| `$[?value(@..color)]` | ❌ | ValueType not valid in test-expr |

---

## 13 Bookstore End-to-End Examples

**JSON:** (the canonical bookstore — see overview file for full JSON)

| Query | Expected Result |
|---|---|
| `$.store.book[*].author` | `"Nigel Rees"`, `"Evelyn Waugh"`, `"Herman Melville"`, `"J. R. R. Tolkien"` |
| `$..author` | same four authors |
| `$.store.*` | the `book` array, the `bicycle` object |
| `$.store..price` | `8.95`, `12.99`, `8.99`, `22.99`, `399` |
| `$..book[2]` | the third book object |
| `$..book[2].author` | `"Herman Melville"` |
| `$..book[2].publisher` | *(empty)* — no such member |
| `$..book[-1]` | the last book object |
| `$..book[0,1]` | first two book objects |
| `$..book[:2]` | first two book objects |
| `$..book[?@.isbn]` | Moby Dick and Lord of the Rings |
| `$..book[?@.price<10]` | Sayings of the Century and Moby Dick |
| `$..*` | all member values and array elements recursively |

---

## 14 Edge Cases for Implementers

### 14.1 Empty Queries
- `$` applied to any value → one-node nodelist of the root.
- Query with segments that match nothing → empty nodelist (not an error).

### 14.2 Step = 0 in Slices
- `$[::0]` → empty result (no error).

### 14.3 Out-of-Range Indices
- `$[999]` on a 3-element array → empty (no error).
- `$[-999]` on a 3-element array → empty (no error).

### 14.4 Type Mismatches in Selectors
- Name selector on an array → empty.
- Index selector on an object → empty.
- Wildcard on a primitive → empty.
- Filter on a primitive → empty.

### 14.5 Duplicate Object Keys
- Behavior is **unpredictable** per RFC 8259 Section 4. Implementations should
  not rely on specific behavior.

### 14.6 Non-I-JSON Numbers
- Numbers outside the I-JSON exact range `[-(2^53)+1, (2^53)-1]` in integer
  positions make the query **invalid** (must raise an error).
- In comparison contexts, non-I-JSON numbers may use implementation-specific
  equality/ordering.

### 14.7 Unicode
- String comparison uses **identity** of Unicode scalar value sequences.
- No normalization (NFC, NFD, etc.) is applied.
- Queries must be valid UTF-8 sequences of Unicode scalar values.
