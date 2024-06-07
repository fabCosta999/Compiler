using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Globalization;

// forzare la cancellazione delle variabili da schermo quando vengono deallocate
// TESTtestTESTtest


// struct
// gestione double instruction  -> inizializzazione struct
// controlli coerenza funzioni  -> provato ma non va
// gestione dei float nelle variabili
// gestione di tutte le dimensioni di dato
// puntatori e vettori
// parentesizzazione
// funzioni di stdio
// funzioni di stdlib
// funzioni di string
// funzioni di ctype
// funzioni di math


// BUG: le variabili locali dichiarate nei for non vengono deallocate

public class UIscript : MonoBehaviour
{
    public enum State { writing, running };

    public static GameObject prefabCell, prefabVariable;
    public static Vector2 start;
    public static Transform variables;
    public static float scale, offset;

    public GameObject prefabCellE, prefabVariableE;
    public Transform variablesE;
    public TMP_InputField inputTextArea;
    public TextMeshProUGUI compileButtonText;
    public Transform cells;
    public Vector2 startE;
    public State state;
    public int memorySize;
    public float offsetE;
    private enum TypeOfScopes { local, function };


    private Dictionary<Tuple<string, int>, List<int>> labelPaths;
    private Dictionary<string, Tuple<int, int>> functionsIndexes;
    private List<Stack<List<int>>> cyclePaths;
    private List<Stack<List<int>>> switchPaths;
    private List<Dictionary<string, int>> variableToIndex;
    private List<List<Variable>> variablesStack;
    private List<List<string>> variablesNames;
    private List<List<int>> programsPath;
    private List<Transformer.Function> functions;
    private List<object> program, currentProgram, indexes;
    private List<int> functionsStackIndexes;
    private Stack<int> returnIndexes;
    private List<TypeOfScopes> typeOfScopes;
    private Stack<Value> functionsResults;
    private GameObject[] memoryCells;
    private bool[] memoryCellsStatus; // true: occupata
    private string[] cellValues;
    private int index;
    private string programText;


    private void Start()
    {
        // da migliorare fa schifo
        prefabVariable = prefabVariableE;
        prefabCell = prefabCellE;
        variables = variablesE;
        start = startE;
        offset = offsetE;

        memoryCells = new GameObject[memorySize];
        memoryCellsStatus = new bool[memorySize];
        cellValues = new string[memorySize];
        scale = 1;
        Variable.memorySize = memorySize;
        Vector2 position = start;
        for (int i=0; i<memorySize; i++)
        {
            memoryCells[i] = Instantiate<GameObject>(prefabCell, position, Quaternion.identity, cells);
            memoryCells[i].transform.localScale = new Vector3(1f, scale);
            memoryCells[i].transform.Find("Address").GetComponentInChildren<TextMeshProUGUI>().text = $"0x{i:X2}";
            cellValues[i] = Convert.ToString(UnityEngine.Random.Range(0, 256), 2).PadLeft(8, '0');
            memoryCells[i].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = cellValues[i];
            position = new Vector2(position.x, position.y + offset * scale);
        }
        Variable.cells = cellValues;
        state = State.writing;
        inputTextArea.interactable = true;
        compileButtonText.text = "compila";
    }

    private void Update()
    {
        if (state == State.running && Input.GetKey(KeyCode.Q) && scale > 0.3f) scale -= 0.01f;
        if (state == State.running && Input.GetKey(KeyCode.W) && scale < 2f) scale += 0.01f;

        Vector2 position = start;
        for (int i = 0; i < memorySize; i++)
        {
            memoryCells[i].transform.position = position;
            memoryCells[i].transform.localScale = new Vector3(1f, scale);
            position = new Vector2(position.x, position.y + offset * scale);   
        }
        if (variablesStack is not null)
            foreach (List<Variable> l in variablesStack)
                foreach (Variable v in l)
                    v.RenderVariable();
    }

    public void Compile()
    {
        switch (state)
        {
            case State.writing:
                programText = inputTextArea.text.Replace("\r", "").Replace("\v", "");
                state = State.running;
                compileButtonText.text = "modifica";
                inputTextArea.interactable = false;
                Tuple<List<object>, List<object>, Dictionary<string, Tuple<int, int>>, List<Transformer.Function>, Dictionary<Tuple<string, int>, List<int>>> ris = Transformer.Compile(programText);
                program = ris.Item1;
                indexes = ris.Item2;
                functionsIndexes = ris.Item3;
                functions = ris.Item4;
                labelPaths = ris.Item5;
                Debug.Log(Stringify(program));
                currentProgram = program;
                functionsStackIndexes = new List<int>();
                programsPath = new List<List<int>> { new List<int>() };
                index = -1;
                variablesNames = new List<List<string>> { new List<string>() };
                variablesStack = new List<List<Variable>> { new List<Variable>() };
                variableToIndex = new List<Dictionary<string, int>> { new Dictionary<string, int>() };
                functionsResults = new Stack<Value>();
                typeOfScopes = new List<TypeOfScopes> { TypeOfScopes.function };
                returnIndexes = new Stack<int>();
                cyclePaths = new List<Stack<List<int>>> { new Stack<List<int>>() };
                switchPaths = new List<Stack<List<int>>> { new Stack<List<int>>() };
                for (int i = 0; i < memoryCellsStatus.Length; i++)
                    memoryCellsStatus[i] = false;
                HighlightText();
                break;
            case State.running:
                state = State.writing;
                inputTextArea.interactable = true;
                inputTextArea.text = programText;
                compileButtonText.text = "compila";
                break;
        }
    }

    private void HighlightText()
    {
        inputTextArea.text = programText;
        int i1, i2;
        List<object> pos = FollowCurrentPath(indexes);
        if (index + 1 == pos.Count) return;
        if (pos[index+1] is Tuple<int, int>)
        {
            i1 = ((Tuple<int, int>)pos[index+1]).Item1;
            i2 = ((Tuple<int, int>)pos[index+1]).Item2;
            inputTextArea.text = programText.Substring(0, i1) + "<color=blue>" + programText.Substring(i1, i2 - i1) + "</color>" + programText.Substring(i2);
        }
        else 
            switch ((string)((List<object>)pos[index + 1])[0])
            {
                case "functionCall": break;
                case "if":
                case "while":
                case "switch":
                    i1 = ((Tuple<int, int>)((List<object>)pos[index + 1])[1]).Item1;
                    i2 = ((Tuple<int, int>)((List<object>)pos[index + 1])[1]).Item2;
                    inputTextArea.text = programText.Substring(0, i1) + "<color=blue>" + programText.Substring(i1, i2 - i1) + "</color>" + programText.Substring(i2);
                    break;
                default: break;
            }
    }

    private List<object> FollowCurrentPath(List<object> l)
    {
        List<object> o = functionsStackIndexes.Count == 0 ? l : (List<object>)((List<object>)l[functionsStackIndexes[functionsStackIndexes.Count - 1]])[2];
        foreach (int n in programsPath[programsPath.Count-1])
            o = (List<object>)o[n];
        return o;
    }

    private static string Stringify(object obj)
    {
        if (obj is string) return (string)obj;
        if (obj is double) return ((double)obj).ToString();
        if (obj is int) return ((int)obj).ToString();
        if (obj is Tuple<int, int>)
            return "(" + ((Tuple<int, int>)obj).Item1.ToString() + ", " + ((Tuple<int, int>)obj).Item2.ToString() + ")";
        string ris = "[";
        foreach (object o in (List<object>)obj)
            ris += Stringify(o) + ",";
        return ris.Length == 1 ? ris + "]" : ris.Substring(0, ris.Length - 1) + "]";
    }

    public void Run1()
    {
        if (state == State.writing) return;
        Value ris;
        string name;
        int i, id;
        List<object> typeList;
        List<int> pathAndIndex;
        string type;
        index++;
        Debug.Log(((List<object>)currentProgram[index])[0]);
        switch (((List<object>)currentProgram[index])[0])
        {
            case "function":
                List<object> currentInstruction = new List<object>((List<object>)currentProgram[index]);
                name = (string)currentInstruction[1];
                NewFunctionScope(functionsIndexes[name].Item1);
                i = 0;
                foreach (Tuple<string, string, int> t in functions[functionsIndexes[name].Item2].parameters)
                {
                    AllocateVariable(t.Item1, t.Item2, t.Item3);
                    ris = Evaluate((List<object>) ((List<object>)currentInstruction[2])[i]);
                    int varIndex = variablesStack[variablesStack.Count - 1].Count - 1;
                    WriteCells(variablesStack[variablesStack.Count - 1][varIndex].Write(ris.value));
                    i++;
                }
                break;
            case "return":
                if (((List<object>)currentProgram[index])[1] is string)
                    ReturnFromFunction(new Value(false, (double)UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
                else
                    ReturnFromFunction(Evaluate((List<object>)((List<object>)currentProgram[index])[1]));
                break;
            case "declarations":
                typeList = (List<object>)((List<object>)currentProgram[index])[1];
                type = Transformer.TypeString(typeList);
                foreach (object o in (List<object>)((List<object>)currentProgram[index])[2])
                {
                    name = (string)((List<object>)o)[0];
                    if (variablesNames.Count > 0 && variablesNames[variablesNames.Count - 1].Contains(name))
                    {
                        Debug.LogError("variabile dichiarata due volte");
                        return;
                    }
                    int pointers = (int)((List<object>)o)[1];
                    if (type[2] == 'v' && pointers == 0)
                    {
                        Debug.LogError("variabile void");
                        return;
                    }

                    AllocateVariable(name, type, pointers);
                    int varIndex = variablesStack[variablesStack.Count - 1].Count - 1;
                    if (((string)((List<object>)((List<object>)o)[2])[0]).Equals("void")) return;

                    ris = Evaluate((List<object>)((List<object>)o)[2]);
                    WriteCells(variablesStack[variablesStack.Count - 1][varIndex].Write(ris.value));
                }
                break;
            case "assignment":
                ris = Evaluate((List<object>)((List<object>)currentProgram[index])[2]);
                switch (((List<object>)((List<object>)currentProgram[index])[1])[0])
                {
                    case "variable":
                        name = (string)((List<object>)((List<object>)currentProgram[index])[1])[1];
                        Tuple<int, int> variableIndex = FindVariable(name);
                        WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Write(ris.value));
                        break;
                    default:
                        break;
                }
                break;
            case "functionDefinition":
            case "functionPrototype":
                break;
            case "if":
                ris = Evaluate((List<object>)((List<object>)currentProgram[index])[1]);
                if (ris.floatingPoint)
                {
                    Debug.LogError("valutazione della condizione floating point, non valida");
                    return;
                }
                if (ris.value != 0f)
                    NewLocalScope(2);
                else if ((string)((List<object>)currentProgram[index])[3] == "else")
                    NewLocalScope(4);
                break;
            case "while":
                ris = Evaluate((List<object>)((List<object>)currentProgram[index])[1]);
                if (ris.floatingPoint)
                {
                    Debug.LogError("valutazione della condizione floating point, non valida");
                    return;
                }
                pathAndIndex = new List<int>(programsPath[programsPath.Count - 1]){index};
                cyclePaths[cyclePaths.Count - 1].Push(pathAndIndex);
                if (ris.value != 0f)
                    NewLocalScope(2);
                break;
            case "continue": 
                if (cyclePaths[cyclePaths.Count - 1].Count == 0)
                {
                    Debug.LogError("continue senza un ciclo associato");
                    return;
                }
                pathAndIndex = cyclePaths[cyclePaths.Count - 1].Pop();
                index = pathAndIndex[pathAndIndex.Count - 1];
                pathAndIndex.RemoveAt(pathAndIndex.Count - 1);
                programsPath[programsPath.Count - 1] = pathAndIndex;
                currentProgram = FollowCurrentPath(program);
                pathAndIndex = new List<int>(programsPath[programsPath.Count - 1]) { index };
                cyclePaths[cyclePaths.Count - 1].Push(pathAndIndex);
                NewLocalScope(2);
                index = currentProgram.Count - 3;
                break;
            case "endOfIteration":
                if (cyclePaths[cyclePaths.Count - 1].Count == 0)
                {
                    Debug.LogError("fine iterazione senza un ciclo associato");
                    return;
                }
                pathAndIndex = cyclePaths[cyclePaths.Count - 1].Pop();
                index = pathAndIndex[pathAndIndex.Count - 1] - 1;
                pathAndIndex.RemoveAt(pathAndIndex.Count - 1);
                programsPath[programsPath.Count - 1] = pathAndIndex;
                currentProgram = FollowCurrentPath(program);
                break;
            case "break": 
                if (cyclePaths[cyclePaths.Count - 1].Count == 0 && switchPaths[switchPaths.Count -1].Count == 0)
                {
                    Debug.LogError("break senza un ciclo/switch associato");
                    return;
                }
                if (cyclePaths[cyclePaths.Count - 1].Count > switchPaths[switchPaths.Count - 1].Count)
                {
                    pathAndIndex = cyclePaths[cyclePaths.Count - 1].Pop();
                    pathAndIndex.RemoveAt(pathAndIndex.Count - 1);
                }
                else
                    pathAndIndex = switchPaths[switchPaths.Count - 1].Pop();
                programsPath[programsPath.Count - 1] = pathAndIndex;
                currentProgram = FollowCurrentPath(program);
                ExitLocalScope();
                break;
            case "switchContinue":
                id = programsPath[programsPath.Count - 1][programsPath[programsPath.Count - 1].Count-1];
                ExitLocalScope();
                if (((List<object>)currentProgram[index]).Count >= id+3) NewLocalScope(id + 2);
                break;
            case "switch":
                Value ris2;
                ris = Evaluate((List<object>)((List<object>)currentProgram[index])[1]);
                if (ris.floatingPoint)
                {
                    Debug.LogError("valutazione della condizione floating point, non valida");
                    return;
                }
                for (int j=2; j<((List<object>)currentProgram[index]).Count; j+=2)
                {
                    List<object> o = (List<object>)((List<object>)currentProgram[index])[j];
                    if (((string)o[0]).Equals("case"))
                    {
                        ris2 = Evaluate((List<object>)o[1]);
                        if (ris2.floatingPoint)
                        {
                            Debug.LogError("valutazione della condizione floating point, non valida");
                            return;
                        }
                        if (ris2.value == ris.value)
                        {
                            pathAndIndex = new List<int>(programsPath[programsPath.Count - 1]) { index, j+1 };
                            switchPaths[switchPaths.Count - 1].Push(pathAndIndex);
                            NewLocalScope(j + 1);
                            break;
                        }
                    }
                    else
                    {
                        pathAndIndex = new List<int>(programsPath[programsPath.Count - 1]) { index, j+1 };
                        switchPaths[switchPaths.Count - 1].Push(pathAndIndex);
                        NewLocalScope(j + 1);
                        break;
                    }
                }
                break;
            case "scope":
                NewLocalScope(1);
                break;
            case "goto":
                name = (string)((List<object>)currentProgram[index])[1];
                if (functionsStackIndexes.Count == 0)
                    id = -1;
                else
                    id = functionsStackIndexes[functionsStackIndexes.Count - 1];
                if (!labelPaths.ContainsKey(new Tuple<string, int>(name, id)))
                {
                    Debug.LogError("goto verso un label inesistente in questa funzione");
                    return;
                }
                pathAndIndex = new List<int>(labelPaths[new Tuple<string, int>(name, id)]);
                int temp = pathAndIndex[pathAndIndex.Count - 1] - 1;
                pathAndIndex.RemoveAt(pathAndIndex.Count - 1);

                while (programsPath[programsPath.Count - 1].Count > 0) ExitLocalScope();
                for (int j = 0; j < pathAndIndex.Count-1; j += 2)
                    NewLocalScope(pathAndIndex[j + 1], pathAndIndex[j]);
                index = temp;
                break;
            default:
                Evaluate((List<object>)currentProgram[index]);
                break;
        }
        if (currentProgram.Count == index + 1) ExitLocalScope();
        HighlightText();
    }

    private void DebugPath() 
    {
        string res = "functions:";
        foreach (int n in functionsStackIndexes)
            res += n.ToString();
        Debug.Log(res);
        res = "program paths:";
        foreach (List<int> l in programsPath)
        {
            foreach (int n in l)
                res += n.ToString();
            res += ";";
        }
        Debug.Log(res);
    }

    private void NewLocalScope(int i, int j = -1)
    {
        if (j == -1) j = index;
        variablesNames.Add(new List<string>());
        variablesStack.Add(new List<Variable>());
        variableToIndex.Add(new Dictionary<string, int>());
        typeOfScopes.Add(TypeOfScopes.local);
        programsPath[programsPath.Count - 1].Add(j);
        programsPath[programsPath.Count - 1].Add(i);
        DebugPath();
        currentProgram = FollowCurrentPath(program);
        index = -1;
    }

    private void NewFunctionScope(int i)
    {
        returnIndexes.Push(index);
        variablesNames.Add(new List<string>());
        variablesStack.Add(new List<Variable>());
        variableToIndex.Add(new Dictionary<string, int>());
        typeOfScopes.Add(TypeOfScopes.function);
        programsPath.Add(new List<int>());
        functionsStackIndexes.Add(i);
        cyclePaths.Add(new Stack<List<int>>());
        switchPaths.Add(new Stack<List<int>>());
        currentProgram = FollowCurrentPath(program);
        index = -1;
    }

    private void ReturnFromFunction(Value result)
    {
        foreach (Variable v in variablesStack[variablesStack.Count - 1])
        {
            List<int> cells = v.GetCells();
            foreach (int n in cells)
                memoryCellsStatus[n] = false;
        }
        variableToIndex.RemoveAt(variableToIndex.Count - 1);
        variablesStack.RemoveAt(variablesStack.Count - 1);
        variablesNames.RemoveAt(variablesNames.Count - 1);
        typeOfScopes.RemoveAt(typeOfScopes.Count - 1);
        programsPath.RemoveAt(programsPath.Count - 1);
        cyclePaths.RemoveAt(cyclePaths.Count - 1);
        switchPaths.RemoveAt(switchPaths.Count - 1);
        functionsStackIndexes.RemoveAt(functionsStackIndexes.Count - 1);
        currentProgram = FollowCurrentPath(program);
        index = returnIndexes.Pop();
        functionsResults.Push(result);
    }

    private void ExitLocalScope() 
    {
        foreach (Variable v in variablesStack[variablesStack.Count - 1])
        {
            List<int> cells = v.GetCells();
            foreach (int n in cells)
                memoryCellsStatus[n] = false;
        }
        variableToIndex.RemoveAt(variableToIndex.Count - 1);
        variablesStack.RemoveAt(variablesStack.Count - 1);
        variablesNames.RemoveAt(variablesNames.Count - 1);
        typeOfScopes.RemoveAt(typeOfScopes.Count - 1);
        int size = programsPath[programsPath.Count - 1].Count;
        DebugPath();
        programsPath[programsPath.Count - 1].RemoveAt(size-1);
        index = programsPath[programsPath.Count - 1][size - 2];
        programsPath[programsPath.Count - 1].RemoveAt(size-2);
        currentProgram = FollowCurrentPath(program);
    }

    private void AllocateVariable(string name, string type, int pointers)
    {
        int size;
        int cell = 0;

        if (pointers > 0)
            size = 1;
        else
        {
            switch (type[2])
            {
                case 'i':
                    switch (type[3])
                    {
                        case '_': size = 4; break;
                        case 's': size = 2; break;
                        case 'l': size = 8; break;
                        default: Debug.LogError("tipo inatteso"); return;
                    }
                    break;
                case 'f':
                    switch (type[3])
                    {
                        case '_': size = 4; break;
                        case 'd': size = 8; break;
                        default: Debug.LogError("tipo inatteso"); return;
                    }
                    break;
                case 'c': size = 1; break;
                default: Debug.LogError("tipo inatteso"); return;
            }
        }
        for (int i = 0; i < memorySize; i += size)
        {
            bool flag = true;
            for (int j = 0; j < size; j++)
                if (memoryCellsStatus[i + j])
                {
                    flag = false;
                    break;
                }
            if (flag)
            {
                cell = i;
                for (int j = 0; j < size; j++)
                    memoryCellsStatus[i + j] = true;
                break;
            }
        }

        variablesNames[variablesNames.Count - 1].Add(name);
        variablesStack[variablesStack.Count - 1].Add(new Variable(name, type, pointers, cell, size));
        variableToIndex[variablesStack.Count - 1].Add(name, variablesStack[variablesStack.Count - 1].Count - 1);

    }


    private void WriteCells(Data d)
    {
        if (d.size == 1)
        {
            cellValues[d.index] = d.text;
            memoryCells[d.index].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = d.text;
        }
        else if (d.size == 4)
        {
            cellValues[d.index] = d.text.Substring(0, 8);
            memoryCells[d.index].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = cellValues[d.index];
            cellValues[d.index+1] = d.text.Substring(8, 8);
            memoryCells[d.index+1].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = cellValues[d.index+1];
            cellValues[d.index+2] = d.text.Substring(16, 8);
            memoryCells[d.index+2].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = cellValues[d.index+2];
            cellValues[d.index+3] = d.text.Substring(24, 8);
            memoryCells[d.index+3].transform.Find("Data").GetComponentInChildren<TextMeshProUGUI>().text = cellValues[d.index+3];
        }
    }

    private Value Evaluate(List<object> l)
    {
        Value val1, val2, val3;
        Tuple<int, int> variableIndex;
        string name;
        switch ((string)l[0])
        {
            case "int": return new Value(false, double.Parse((string)l[1]));
            case "float": return new Value(true, double.Parse((string)l[1], CultureInfo.InvariantCulture));
            case "char": return new Value(false, (double)(int)((string)l[1])[0]);
            case "+":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(val1.floatingPoint || val2.floatingPoint, val1.value + val2.value);
            case "*":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(val1.floatingPoint || val2.floatingPoint, val1.value * val2.value);
            case "-":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(val1.floatingPoint || val2.floatingPoint, val1.value - val2.value);
            case "/":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return val1.floatingPoint || val2.floatingPoint ?
                    new Value(true, val1.value / val2.value) :
                    new Value(false, (int)val1.value / (int)val2.value);
            case "%":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione modulo con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value % (int)val2.value);
            case ">=":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value >= val2.value ? 1 : 0);
            case "<=":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value <= val2.value ? 1 : 0);
            case ">":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value > val2.value ? 1 : 0);
            case "<":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value < val2.value ? 1 : 0);
            case "==":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value == val2.value ? 1 : 0);
            case "!=":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                return new Value(false, val1.value != val2.value ? 1 : 0);
            case "&&":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione logica con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value!=0 && (int)val2.value!=0 ? 1: 0);
            case "||":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione logica con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value != 0 || (int)val2.value != 0 ? 1 : 0);
            case "&":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione bit a bit con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value & (int)val2.value);
            case "|":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione bit a bit con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value | (int)val2.value);
            case "^":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                if (val1.floatingPoint || val2.floatingPoint)
                {
                    Debug.LogError("operazione bit a bit con float, non valida");
                    return new Value(true, 0.0);
                }
                return new Value(false, (int)val1.value | (int)val2.value);
            case "=":
                val1 = Evaluate((List<object>)l[2]);
                if (((string)((List<object>)l[1])[0]).Equals("variable"))
                {
                    name = (string)((List<object>)l[1])[1];
                    variableIndex = FindVariable(name);
                    if (variableIndex == null)
                    {
                        Debug.LogError("variabile insesitente");
                        return new Value(true, 0.0);
                    }
                    WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Write(val1.value));
                    return variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
                }
                else
                {
                    Debug.LogError("assegnazione a una non variabile, non valida");
                    return new Value(true, 0.0);
                }
            case "?":
                val1 = Evaluate((List<object>)l[1]);
                val2 = Evaluate((List<object>)l[2]);
                val3 = Evaluate((List<object>)l[3]);
                if (val1.floatingPoint)
                {
                    Debug.LogError("operazione logica con float, non valida");
                    return new Value(true, 0.0);
                }
                return val1.value != 0 ? val2 : val3;
            case "cast":
                val1 = Evaluate((List<object>)l[2]);
                name = Transformer.TypeString((List<object>)((List<object>)l[1])[0]);
                int pointers = (int)((List<object>)l[1])[1];
                if (name[2] == 'f' && pointers == 0) return new Value(true, val1.value);
                return new Value(false, (double)((int)val1.value));
            case "variable":
                 name = (string)l[1];
                 variableIndex = FindVariable(name);
                 if (variableIndex == null)
                 {
                     Debug.LogError("variabile insesitente");
                     return new Value(true, 0.0);
                 }
                 return variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
            case "++pre":
                variableIndex = FindVariable((string)((List<object>)l[1])[1]);
                if (variableIndex == null)
                {
                    Debug.LogError("variabile insesitente");
                    return new Value(true, 0.0);
                }
                WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Increment());
                return variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
            case "++post":
                variableIndex = FindVariable((string)((List<object>)l[1])[1]);
                if (variableIndex == null)
                {
                    Debug.LogError("variabile insesitente");
                    return new Value(true, 0.0);
                }
                val1 = variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
                WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Increment());
                return val1;
            case "--pre":
                variableIndex = FindVariable((string)((List<object>)l[1])[1]);
                if (variableIndex == null)
                {
                    Debug.LogError("variabile insesitente");
                    return new Value(true, 0.0);
                }
                WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Decrement());
                return variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
            case "--post":
                variableIndex = FindVariable((string)((List<object>)l[1])[1]);
                if (variableIndex == null)
                {
                    Debug.LogError("variabile insesitente");
                    return new Value(true, 0.0);
                }
                val1 = variablesStack[variableIndex.Item1][variableIndex.Item2].GetValue();
                WriteCells(variablesStack[variableIndex.Item1][variableIndex.Item2].Decrement());
                return val1;
            case "functionCall":
                return functionsResults.Pop();

        }
        Debug.LogError("espressione insesitente");
        return new Value(true, 0.0);
    }

    private Tuple<int, int> FindVariable(String name)
    {
        for (int i = variableToIndex.Count - 1; i > 0; i--)
        {
            if (variableToIndex[i].ContainsKey(name))
                return new Tuple<int, int>(i, variableToIndex[i][name]);
            if (typeOfScopes[i] == TypeOfScopes.function)
                break;
        }
        if (variableToIndex[0].ContainsKey(name))
            return new Tuple<int, int>(0, variableToIndex[0][name]);
        Debug.LogError($"variabile {name} inesistente");
        return null;
    }

    public struct Value
    {
        public bool floatingPoint;
        public double value;

        public Value(bool fp, double val)
        {
            floatingPoint = fp;
            value = val;
        }
    }

    private class Variable
    {
        static public string[] cells;
        private string name;
        private string type;
        private int pointers;
        private int cell;
        private int size;
        public static int memorySize;
        public GameObject image;


        public Variable(string _name, string _type, int _pointers, int _cell, int _size)
        {
            name = _name;
            type = _type;
            pointers = _pointers;
            cell = _cell;
            size = _size;

            image = Instantiate<GameObject>(UIscript.prefabVariable, UIscript.start, Quaternion.identity, UIscript.variables);
        }

        public void Deallocate()
        {
            Destroy(image);
        }

        public void RenderVariable()
        {
            Vector3 scale = new Vector3(1f, UIscript.scale * size * 0.9f) / 1.5f;
            Vector2 position = start + Vector2.up * UIscript.offset * UIscript.scale * (cell + size / 2 - 0.5f);
            image.transform.position = position;
            image.transform.localScale = scale;
            image.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = name;
            double val = ReadCellsValue();
            string ris = "";
            switch (type[2])
            {
                case 'i':
                    ris = ((int)val).ToString();
                    break;
                case 'f':
                    ris = val.ToString();
                    break;
                case 'c':
                    ris = ((char)(int)val).ToString();
                    break;
            }
            image.transform.Find("Value").GetComponent<TextMeshProUGUI>().text = ris;
        }
        

        private double ReadCellsValue() 
        {
            string binary = "";
            string positives;
            for (int i = 0, c = cell; i < size; i++, c++)
                binary += cells[c];
            // per adesso solo casi dimensione standard (f:32, i:32, c:8, p:8) 
            // da aggiornare
            if (pointers > 0) return (double) Convert.ToInt32(binary, 2);
            switch (type[2])
            {
                case 'i':
                    positives = binary.Substring(1);
                    return (double)(Convert.ToInt32(positives, 2) - (binary[0] == '1' ? Math.Pow(2, 31) : 0));
                case 'c':
                    return (double)Convert.ToInt32(binary, 2);
                case 'f': return 0.0; // ...
                default: return 0.0;
            }
        }
        

        public Value GetValue() => new Value(type[2] == 'f', ReadCellsValue());

        public List<int> GetCells()
        {
            List<int> l = new List<int>();
            for (int i = 0; i < size; i++) l.Add(cell + i);
            return l;
        }

        public Data Increment() => Write(ReadCellsValue() + 1);
        

        public Data Decrement() => Write(ReadCellsValue() - 1);

        public Data Write(double value)
        {
            if (pointers > 0)
            {
                int pval = (int)value;
                if (pval > memorySize || pval < memorySize)
                    Debug.LogError("puntatore fuori range memoria");
                return new Data(cell, size, Convert.ToString(pval % memorySize, 2).PadLeft(8, '0'));
            }
            else
            {
                switch (type[2]) 
                {
                    case 'i':
                        int ival = (int)value;
                        if (ival < int.MinValue || ival > int.MaxValue)
                            Debug.LogError("intero in overflow");
                        return new Data(cell, size, Convert.ToString(ival, 2).PadLeft(32, '0'));
                    case 'c':
                        char cval = (char)(int)value;
                        return new Data(cell, size, Convert.ToString((int)cval, 2).PadLeft(8, '0'));
                    case 'f':
                        float fval = (float)value;
                        return new Data(cell, size, Convert.ToString(BitConverter.SingleToInt32Bits(fval), 2).PadLeft(32, '0'));
                }
            }
            Debug.LogError("errore rappresentazione");
            return new Data(cell, 1, "00000000");
        }
    }

    public struct Data
    {
        public int index, size;
        public string text;

        public Data(int _index, int _size, string _text)
        {
            index = _index;
            size = _size;
            text = _text;
        }
    }
}
