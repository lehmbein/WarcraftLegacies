"""Cross-class reference audit for the WL Ai/ folder.
Catches the slip that `dotnet build` may pass but json-to-w3x rejects: `AiConfig.X` where X lives in AiGraph,
or a private member used from another class. Usage: python3 check_refs.py <dir-with-Ai*.cs>"""
import sys, glob, os
import tree_sitter_c_sharp as tscs, tree_sitter as ts

parser = ts.Parser(ts.Language(tscs.language()))
files = sorted(glob.glob(os.path.join(sys.argv[1] if len(sys.argv) > 1 else ".", "*.cs")))
members = {}   # class -> {name: is_public}
refs = []      # (file, line, from_class, cls, member)

def txt(n, src): return src[n.start_byte:n.end_byte].decode("utf-8")

def mods(n, src):
    return {txt(c, src) for c in n.children if c.type == "modifier"}

def collect(n, src, cls=None):
    if n.type == "class_declaration":
        name = txt(n.child_by_field_name("name"), src)
        members.setdefault(name, {})
        if cls:
            members[cls][name] = bool(mods(n, src) & {"public", "internal"})
        for c in n.children: collect(c, src, name)
        return
    if cls and n.type in ("field_declaration", "property_declaration", "method_declaration", "event_field_declaration"):
        pub = bool(mods(n, src) & {"public", "internal"})
        if n.type == "field_declaration" or n.type == "event_field_declaration":
            for d in n.children:
                if d.type == "variable_declaration":
                    for v in d.children:
                        if v.type == "variable_declarator":
                            members[cls][txt(v.child_by_field_name("name") or v.children[0], src)] = pub
        else:
            members[cls][txt(n.child_by_field_name("name"), src)] = pub
    for c in n.children: collect(c, src, cls)

def find_refs(n, src, f, cls=None):
    if n.type == "class_declaration":
        cls = txt(n.child_by_field_name("name"), src)
    if n.type == "member_access_expression":
        e, nm = n.child_by_field_name("expression"), n.child_by_field_name("name")
        if e is not None and nm is not None and e.type == "identifier":
            refs.append((f, n.start_point[0] + 1, cls, txt(e, src), txt(nm, src)))
    for c in n.children: find_refs(c, src, f, cls)

trees = []
for f in files:
    src = open(f, "rb").read()
    t = parser.parse(src)
    trees.append((f, src, t))
    collect(t.root_node, src)
for f, src, t in trees:
    find_refs(t.root_node, src, os.path.basename(f))

# 0.34.3: CSharp.lua builds every static field that has a non-literal initializer ("= new()", a method call...)
# inside the class's static constructor, which may use only ~55 Lua upvalues. Fields past that limit are silently
# left nil at runtime (this broke portals and every bot attack in v34-v34.2). Count them per class.
CTOR_LIMIT = 45
LITERALS = {"integer_literal", "real_literal", "boolean_literal", "string_literal", "null_literal",
            "character_literal", "verbatim_string_literal"}
ctor_fields = {}

def count_ctor(n, src, cls=None):
    if n.type == "class_declaration":
        cls = txt(n.child_by_field_name("name"), src)
    if cls and n.type == "field_declaration":
        ms = mods(n, src)
        if "static" in ms and "const" not in ms:
            for d in n.children:
                if d.type != "variable_declaration":
                    continue
                for v in d.children:
                    if v.type != "variable_declarator":
                        continue
                    vals = [c for c in v.children if c.is_named and c.type not in ("identifier",)]
                    val = vals[-1] if vals else None
                    if val is not None and val.type == "equals_value_clause":
                        val = val.named_children[-1] if val.named_children else None
                    if val is not None and val.type not in LITERALS and not (
                            val.type == "prefix_unary_expression" and val.named_children
                            and val.named_children[0].type in LITERALS):
                        ctor_fields.setdefault(cls, []).append(txt(v.children[0], src))
    for c in n.children:
        count_ctor(c, src, cls)

for f, src, t in trees:
    count_ctor(t.root_node, src)

bad = 0
for cls, names in sorted(ctor_fields.items()):
    if len(names) > CTOR_LIMIT:
        print(f"TOO MANY INITIALIZED STATIC FIELDS  {cls}: {len(names)} (limit {CTOR_LIMIT}) -- "
              f"create the newest ones in an Init method instead; last: {', '.join(names[CTOR_LIMIT:])}")
        bad += 1
print("initialized static fields per class: "
      + ", ".join(f"{c} {len(n)}" for c, n in sorted(ctor_fields.items(), key=lambda kv: -len(kv[1]))[:5]))

for f, line, frm, cls, m in refs:
    if cls not in members:
        continue  # not one of our classes (WC3 API, MacroTools, locals...)
    if m not in members[cls]:
        owners = [c for c, ms in members.items() if m in ms]
        print(f"MISSING  {f}:{line}  {cls}.{m}  (declared in: {owners or 'nowhere'})"); bad += 1
    elif frm != cls and not members[cls][m]:
        print(f"PRIVATE  {f}:{line}  {cls}.{m} used from {frm}"); bad += 1
print(f"checked {sum(1 for r in refs if r[3] in members)} cross-class references in {len(files)} files: "
      + ("OK" if bad == 0 else f"{bad} problem(s)"))
sys.exit(1 if bad else 0)
