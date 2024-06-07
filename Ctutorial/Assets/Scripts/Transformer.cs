using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// funzioni che ritornano puntatori

public static class Transformer
{
    public static Tuple<List<object>, List<object>, Dictionary<string, Tuple<int, int>>, List<Function>, Dictionary<Tuple<string, int>, List<int>>> Compile(string programText)
    {
        Dictionary<string, Tuple<int, int>> functionsIndexes = new Dictionary<string, Tuple<int, int>>();
        List<Function> functions = new List<Function>();
        Dictionary<Tuple<string, int>, List<int>> labelPaths = new Dictionary<Tuple<string, int>, List<int>>();
        Tuple<List<object>, List<object>> ris = Compiler.Parse(AddParenthesis(programText));
        List<object> program = ris.Item1;
        List<object> indexes = ris.Item2;
        InsertFunctionCalls(program, indexes);
        SplitTwoInstructions(program, indexes);
        FunctionPrototypes(program, functionsIndexes, functions);
        HandleLabels(program, indexes, -1, new List<int> { }, labelPaths);
        return new Tuple<List<object>, List<object>, Dictionary<string, Tuple<int, int>>, List<Function>, Dictionary<Tuple<string, int>, List<int>>>(program, indexes, functionsIndexes, functions, labelPaths);
    }

    private static void SplitTwoInstructions(List<object> program, List<object> indexes)
    {
        int v;
        for (int i = 0; i < program.Count; i += v + 1)
        {
            v = 0;
            if (program[i] is not List<object>) return;
            if (((List<object>)program[i])[0] is not string) return;
            switch ((string)((List<object>)program[i])[0])
            {
                case "twoInstructions":
                    v = 1;
                    List<object> instruction1 = (List<object>)((List<object>)program[i])[1];
                    List<object> instruction2 = (List<object>)((List<object>)program[i])[2];
                    List<object> indexes1 = (List<object>)((List<object>)indexes[i])[1];
                    List<object> indexes2 = (List<object>)((List<object>)indexes[i])[2];
                    program[i] = instruction2;
                    program.Insert(i, instruction1);
                    indexes[i] = indexes2;
                    indexes.Insert(i, indexes1);
                    SplitTwoInstructions((List<object>)program[i], (List<object>)indexes[i]);
                    SplitTwoInstructions((List<object>)((List<object>)program[i + 1])[2], (List<object>)((List<object>)indexes[i + 1])[2]);
                    break;
                case "if":
                    SplitTwoInstructions((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2]);
                    if (((string)((List<object>)program[i])[3]).Equals("else"))
                        SplitTwoInstructions((List<object>)((List<object>)program[i])[4], (List<object>)((List<object>)indexes[i])[4]);
                    break;
                case "while":
                    SplitTwoInstructions((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2]);
                    break;
                case "switch":
                    for (int j = 3; j < ((List<object>)program[i]).Count; j += 2)
                        SplitTwoInstructions((List<object>)((List<object>)program[i])[j], (List<object>)((List<object>)indexes[i])[j]);
                    break;
                case "functionDefinition":
                    SplitTwoInstructions((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2]);
                    break;
                case "scope":
                    SplitTwoInstructions((List<object>)((List<object>)program[i])[1], (List<object>)((List<object>)indexes[i])[1]);
                    break;
                case "label":
                    SplitTwoInstructions(new List<object> { ((List<object>)program[i])[2] }, new List<object> { ((List<object>)indexes[i])[2] });
                    break;
                default:
                    break;
            }
        }
    }

    private static void InsertFunctionCalls(List<object> program, List<object> indexes)
    {
        int v, n;
        List<List<object>> l;
        for (int j = 0; j < program.Count; j += v + 1)
        {
            if (((string)((List<object>)program[j])[0]).Equals("functionDefinition"))
            {
                InsertFunctionCalls((List<object>)((List<object>)program[j])[2], (List<object>)((List<object>)indexes[j])[2]);
                v = 0;
                continue;
            }
            l = FunctionsInExpression((List<object>)program[j]);
            v = l.Count;
            n = 0;
            foreach (List<object> o in l)
            {
                o[0] = "function";
                program.Insert(j + n, o);
                indexes.Insert(j + (n++), o);
            }
        }
    }


    private static List<List<object>> FunctionsInExpression(List<object> l)
    {
        List<List<object>> res = new List<List<object>>();
        List<object> reversedList = new List<object>(l);
        reversedList.Reverse();
        if (l.Count > 0 && l[0] is string && ((string)l[0]).Equals("functionCall"))
        {
            res = FunctionsInExpression((List<object>)l[2]);
            List<object> temp = new List<object>(l);
            l.RemoveAt(2);
            res.Add(temp);
        }
        else
            foreach (object o in reversedList)
                if (o is List<object>)
                    res.AddRange(FunctionsInExpression((List<object>)o));
        return res;
    }

    private static void FunctionPrototypes(List<object> program, Dictionary<string, Tuple<int, int>> functionsIndexes, List<Function> functions)
    {
        string name;
        int id;

        for (int index=0; index<program.Count; index++)
        {
            if (((string)((List<object>)program[index])[0]).Equals("functionDefinition"))
            {
                name = (string)((List<object>)program[index])[1];
                List<object> functionPrototype = (List<object>)((List<object>)program[index])[3];

                if (functionsIndexes.ContainsKey(name))
                {
                    if (functionsIndexes[name].Item1 == index) continue;
                    else
                    {
                        Debug.LogError($"funzione {name} definita due volte");
                        return;
                    }
                }

                functions.Add(FunctionPrototype(name, functionPrototype));
                functionsIndexes.Add(name, new Tuple<int, int>(index, functions.Count - 1));
            }



            else if (((string)((List<object>)program[index])[0]).Equals("functionPrototype"))
            {
                name = (string)((List<object>)((List<object>)program[index])[1])[1];
                if (functionsIndexes.ContainsKey(name)) return; // da controllare se il prototipo coincide
                id = -1;
                for (int i = index + 1; i < program.Count; i++)
                {
                    if (((string)((List<object>)program[i])[0]).Equals("functionPrototype") &&
                        ((string)((List<object>)((List<object>)program[i])[1])[1]).Equals(name))
                    {
                        Debug.LogError($"doppio prototipo per la funzione {name}");
                        return;
                    }
                    if (((string)((List<object>)program[i])[0]).Equals("functionDefinition") && ((string)((List<object>)program[i])[1]).Equals(name))
                    {
                        if (id == -1) id = i;
                        else
                        {
                            Debug.LogError($"funzione {name} definita due volte");
                            return;
                        }
                    }
                }
                if (id == -1)
                {
                    Debug.LogError($"prototipo della funzione {name} senza la relativa definizione");
                    return;
                }

                if (!CompareFunctionPrototypes((List<object>)((List<object>)program[index])[1], (List<object>)((List<object>)program[id])[3]))
                {
                    Debug.LogError($"prototipi diversi per la funzione {name}"); // non funziona 
                    return;
                }
                
                functions.Add(FunctionPrototype(name, (List<object>)((List<object>)program[id])[3]));
                functionsIndexes.Add(name, new Tuple<int, int>(id, functions.Count - 1));
            }
        }
    }

    private static void HandleLabels(List<object> program, List<object> indexes, int functionIndex, List<int> path, Dictionary<Tuple<string, int>, List<int>> labelPaths)
    {
        for (int i = 0; i < program.Count; i++)
        {
            if (program[i] is not List<object>) return;
            if (((List<object>)program[i])[0] is not string) return;
            switch ((string)((List<object>)program[i])[0])
            {
                case "label":
                    HandleLabels(new List<object> { ((List<object>)program[i])[2]}, new List<object> { ((List<object>)indexes[i])[2] }, functionIndex, new List<int>(path), labelPaths);
                    if (labelPaths.ContainsKey(new Tuple<string, int>((string)((List<object>)program[i])[1], functionIndex))){
                        Debug.LogError($"label {((List<object>)program[i])[1]} utilizzato più volte");
                        return;
                    }
                    labelPaths.Add(new Tuple<string, int>((string)((List<object>)program[i])[1], functionIndex), new List<int>(path) { i });
                    program[i] = ((List<object>)program[i])[2];
                    indexes[i] = ((List<object>)indexes[i])[2];
                    break;
                case "if":
                    HandleLabels((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2], functionIndex, new List<int>(path) { i, 2 }, labelPaths);
                    if (((string)((List<object>)program[i])[3]).Equals("else"))
                        HandleLabels((List<object>)((List<object>)program[i])[4], (List<object>)((List<object>)indexes[i])[4], functionIndex, new List<int>(path) { i, 4 }, labelPaths);
                    break;
                case "while":
                    HandleLabels((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2], functionIndex, new List<int>(path) { i, 2 }, labelPaths);
                    break;
                case "switch":
                    for (int j = 3; j < ((List<object>)program[i]).Count; j += 2)
                        HandleLabels((List<object>)((List<object>)program[i])[j], (List<object>)((List<object>)indexes[i])[j], functionIndex, new List<int>(path) { i, j }, labelPaths);
                    break;
                case "functionDefinition":
                    HandleLabels((List<object>)((List<object>)program[i])[2], (List<object>)((List<object>)indexes[i])[2], i, new List<int>(path) { 2 }, labelPaths);
                    break;
                case "scope":
                    HandleLabels((List<object>)((List<object>)program[i])[1], (List<object>)((List<object>)indexes[i])[1], functionIndex, new List<int>(path) { i, 1 }, labelPaths);
                    break;
                default:
                    break;
            }
        }
    }

    private static string AddParenthesis(string program)
    {
        return program;
    }

    private static Function FunctionPrototype(string name, List<object> fp)
    {
        List<object> typeList = (List<object>)fp[0];
        List<Tuple<string, string, int>> lArg = new List<Tuple<string, string, int>>();
        foreach (object o in (List<object>)fp[2])
        {
            List<object> tl = (List<object>)((List<object>)o)[0];
            string t = TypeString(tl);
            int p = (int)((List<object>)((List<object>)o)[1])[0];
            string n = (string)((List<object>)((List<object>)o)[1])[1];
            lArg.Add(new Tuple<string, string, int>(n, t, p));
        }
        return new Function(name, TypeString(typeList), lArg);
    }


    // qualcosa non va
    private static bool CompareFunctionPrototypes(List<object> fp1, List<object> fp2)
    {
        if (!TypeString((List<object>)fp1[0]).Equals(TypeString((List<object>)fp2[0]))) return false;
        if (((List<object>)fp1[2]).Count != ((List<object>)fp2[2]).Count) return false;
        for (int i = 0; i < ((List<object>)fp1[2]).Count; i++)
        {
            if (!TypeString((List<object>)((List<object>)((List<object>)fp1[2])[i])[0]).Equals(TypeString((List<object>)((List<object>)((List<object>)fp2[2])[i])[0])))
                return false;
            if ((int)((List<object>)((List<object>)((List<object>)fp1[2])[i])[1])[0] != (int)((List<object>)((List<object>)((List<object>)fp2[2])[i])[1])[0])
                return false;
        }
        return true;
    }


    public static string TypeString(List<object> typeList)
    {
        char[] typeVector = { '_', '_', '_', '_', '_' };
        foreach (string s in typeList)
        {
            string r = Types.groups[s];
            for (int i = 0; i < 5; i++)
            {
                if (r[i] != '_' && typeVector[i] != '_')
                {
                    Debug.LogError("tipo incompatibile");
                    return "";
                }
                if (r[i] != '_') typeVector[i] = r[i];
            }
        }
        return new string(typeVector);
    }

    public static class Types
    {
        public static Dictionary<string, string> groups = new Dictionary<string, string>
        {
            { "int",  "__i__"},
            { "float",  "__f__"},
            { "char",  "__c__"},
            { "double",  "__fd_"},
            { "long",  "__il_"},
            { "short",  "__is_"},
            { "signed",  "__i_s"},
            { "unsigned",  "__i_u"},
            { "void",  "__v__"},
            { "auto",  "_a___"},
            { "const",  "c____"},
            { "static",  "_s___"},
            { "volatile",  "_v___"},
            { "extern",  "_e___"},
            { "register",  "_r___"}
        };
    }

    public class Function
    {
        private string name;
        private string type;
        public List<Tuple<string, string, int>> parameters;

        public Function(string _name, string _type, List<Tuple<string, string, int>> _parameters)
        {
            name = _name;
            type = _type;
            parameters = _parameters;
        }
    }
}
