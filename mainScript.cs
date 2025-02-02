using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public enum SnailStateEnum { Idle, Runing, Failed, Finished};
public enum SnailCommand{ NoCommand, MoveForward, TurnRight, TurnLeft}
public interface ISnail
{
    SnailCommand Think(bool LeftSensor, bool CenterSensor, bool RightSensor);
    string GetName();
    Color GetColor();
    void Finish();
    void Failed();
}


public class mainScript : MonoBehaviour
{
    public float GridStep=20;
    private Vector2Int Offset = Vector2Int.zero;

    public float CellGapK = 0.05f;

    private Material lineMaterial;

    public Color mainColor = new Color(0f, 1f, 0f, 1f);
    public Color subColor = new Color(0f, 0.5f, 0f, 1f);
    public Color cellColor = Color.cyan;
    public Color StartCellColor = Color.blue;
    public Color FinishCellColor = Color.magenta;
    //public Color snailColor = Color.red;

    private HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
    private RectInt GridMarginI = new RectInt(210, 10, 10, 10);
    private RectInt GridRect = new RectInt(0, 0, 0, 0);
    private Vector2Int screenRes;
    private Rect GridRectF = Rect.zero;

    //private Vector2Int LogP0 = new Vector2Int(10, 130); //, 170, 100);
    private List<string> Logs = new List<string>();

    private bool Dragging;
    private Vector2Int DragCellPos;

    public float Tick = 0.5f;
    private int TicksPassed = 0;
    private float Time1 = 0;
    private float StartTime = 0;
    public UnityEngine.Object[] SnailScripts;
    private ISnail[] Snails;
    private Vector2Int[] SnailPositions;
    private int[] SnailDirections;
    private SnailStateEnum[] SnailStates;
    private float[] SnailTimes;

    private Vector2Int[] SnailDirTempl = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    private bool Runing = false;
    private bool StartCellMode = true;
    private bool FinishCellMode = false;

    private Vector2Int? StartCell = null;
    private Vector2Int? FinishCell = null;

    #region Game Control
    // Start is called before the first frame update
    void Start()
    {
        SnailPositions = new Vector2Int[SnailScripts.Length];
        Snails = new ISnail[SnailScripts.Length];
        SnailDirections = new int[SnailScripts.Length];
        SnailStates = new SnailStateEnum[SnailScripts.Length];
        SnailTimes = new float[SnailScripts.Length];

        InitPlayers();
    }

    void InitPlayers()
    {
        for (int i = 0; i < SnailScripts.Length; i++)
        {
            Snails[i] = CreateInstance(SnailScripts[i].name) as ISnail;
            SnailPositions[i] = new Vector2Int(10, 10);
            SnailDirections[i] = 0;
            SnailStates[i] = SnailStateEnum.Idle;
            SnailTimes[i] = 0;
        }
    }


    // Update is called once per frame
    void Update()
    {
        #region OnScreenResize-Init
        if (screenRes.x!=Screen.width || screenRes.y!=Screen.height)
        {
            screenRes = new Vector2Int(Screen.width, Screen.height);
            GridRect = new RectInt(GridMarginI.x, GridMarginI.y,
                screenRes.x - GridMarginI.x - GridMarginI.width,
                screenRes.y - GridMarginI.y - GridMarginI.height
                );

            GridRectF = new Rect
                (
                    GridRect.x / (float)screenRes.x,
                    GridRect.y / (float)screenRes.y,
                    GridRect.width / (float)screenRes.x,
                    GridRect.height / (float)screenRes.y
                );
        }
        #endregion OnScreenResize-Init
        //Resolution a;

        #region Scene Edit
        if (!Runing && Input.GetMouseButtonDown(0))
        {
            
            Vector2Int mp = Vector2Int.RoundToInt(Input.mousePosition);
            if (GridRect.Contains(mp))
            {
                Vector2Int CellPos = CellPosFromScreenPixel(mp-GridRect.min) - Offset;
                bool HasCell = Cells.Contains(CellPos);
                if (StartCell == CellPos) StartCell = null;
                if (FinishCell == CellPos) FinishCell = null;
                if (HasCell) Cells.Remove(CellPos);

                if (StartCellMode)
                {
                    StartCellMode = false;
                    StartCell = CellPos;
                }
                else
                {
                    if (FinishCellMode)
                    {
                        FinishCellMode = false;
                        if (FinishCell == null ? false : Cells.Contains(FinishCell.Value))
                            Cells.Remove(FinishCell.Value);

                        FinishCell = CellPos;

                    }
                    if (!HasCell)
                        Cells.Add(CellPos);
                }
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            Dragging = true;
            Vector2Int mp = Vector2Int.RoundToInt(Input.mousePosition)-GridRect.min;
            DragCellPos = CellPosFromScreenPixel(mp) - Offset;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            Dragging = false;
        }
        else if (Dragging)
        {
            Vector2Int mp = Vector2Int.RoundToInt(Input.mousePosition) - GridRect.min;
            Offset = CellPosFromScreenPixel(mp) - DragCellPos;
        }
        Vector2 MouseScrollV = Input.mouseScrollDelta;
        GridStep = (int)Mathf.Clamp(GridStep + MouseScrollV.y, 1, 10000);
        #endregion Scene Edit

        #region Process Game
        if (Runing)
            if (Time.time> Time1)
            {
                TicksPassed++;
                for (int i=0; i<Snails.Length; i++)
                    ProcessSnail(i);
                Time1 = Time.time + Tick;
            }
        #endregion Process Game
    }
    private static object CreateInstance(string className)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var type = assembly.GetTypes()
            .First(t => t.Name == className);
        return Activator.CreateInstance(type);
    }
    private void ProcessSnail(int SnailI)
    {
        ISnail s = Snails[SnailI];
        if (s != null)
        {
            SnailStateEnum snailState = SnailStates[SnailI];
            if (snailState == SnailStateEnum.Idle)
                snailState = SnailStateEnum.Runing;
            else if (snailState == SnailStateEnum.Runing)
            {
                if (SnailStates[SnailI] == SnailStateEnum.Idle) SnailStates[SnailI] = SnailStateEnum.Runing;
                Vector2Int sPos = SnailPositions[SnailI];
                int DirI = SnailDirections[SnailI];
                Vector2Int DirV = SnailDirTempl[DirI];

                if (sPos == FinishCell)
                {
                    snailState = SnailStateEnum.Finished;
                    SnailTimes[SnailI] = Time1-StartTime;
                    s.Finish();
                    Logs.Add("Step " + GetStepsPassed()+">"+s.GetName() + " Finished");
                }
                else if (sPos != StartCell && !Cells.Contains(sPos))
                {
                    s.Failed();
                    Logs.Add("Step " + GetStepsPassed() + ">" + s.GetName() + " Failed");
                    snailState = SnailStateEnum.Failed;
                }
                else
                {
                    SnailCommand c = s.Think(
                        Cells.Contains(sPos + new Vector2Int(-DirV.y, DirV.x)),
                        Cells.Contains(sPos + DirV),
                        Cells.Contains(sPos + new Vector2Int(DirV.y, -DirV.x)));
                    int moveD = 0;

                    switch (c)
                    {
                        case SnailCommand.MoveForward:
                            moveD = 1;
                            break;
                        case SnailCommand.TurnRight:
                            DirI++;
                            if (DirI > 3)
                                DirI = 0;
                            break;
                        case SnailCommand.TurnLeft:
                            DirI--;
                            if (DirI < 0)
                                DirI = 3;
                            break;
                    }
                    SnailDirections[SnailI] = DirI;
                    SnailPositions[SnailI] += moveD * SnailDirTempl[DirI];

                }

            } else if (snailState == SnailStateEnum.Failed)
            {
                snailState = SnailStateEnum.Runing;
                SnailDirections[SnailI] = 0;
                SnailPositions[SnailI] = StartCell ?? Vector2Int.zero;
            }

            SnailStates[SnailI] = snailState;
        }
    }
    private Vector2Int CellPosFromScreenPixel(Vector2Int pixel)
    {
        return new Vector2Int((int)(pixel.x / (float)GridStep), (int)(pixel.y / (float)GridStep));
    }

    private string GetStepsPassed()
    {
        return TicksPassed.ToString();
    }

    #endregion Game Control



    #region Graphics
    void OnGUI()
    {
        if (!Runing)
        {
            if (GUI.Button(new Rect(10, 10, 90, 30), "Load"))
                LoadCells("Labyrinth");
            if (GUI.Button(new Rect(110, 10, 90, 30), "Save"))
                SaveCells("Labyrinth");

            Color c0 = GUI.contentColor;
            GUI.contentColor = StartCellMode ? Color.white : Color.grey;
            if (GUI.Button(new Rect(10, 50, 90, 30), "Start Cell"))
            {
                StartCellMode = !StartCellMode;
                FinishCellMode = false;
            }
            GUI.contentColor = FinishCellMode ? Color.white : Color.grey;
            if (GUI.Button(new Rect(110, 50, 90, 30), "Finish Cell"))
            {
                FinishCellMode = !FinishCellMode;
                StartCellMode = false;
            }
            GUI.contentColor = c0;
            if (GUI.Button(new Rect(10, 90, 90, 30), "Start"))
            {
                for (int i = 0; i < SnailPositions.Length; i++)
                {
                    SnailPositions[i] = StartCell ?? Vector2Int.zero;
                    SnailDirections[i] = 0;
                    SnailStates[i] = SnailStateEnum.Idle;
                }
                Logs.Add("--- Start ---");
                Runing = true;
                StartTime = Time.time;
                TicksPassed = 0;
            }
            if (GUI.Button(new Rect(110, 90, 90, 30), "Reset Game"))
            {
                Logs.Clear();
                InitPlayers();
            }
        }
        else
        {
            if (GUI.Button(new Rect(10, 10, 90, 30), "Stop"))
            {
                Runing = false;
                Logs.Add("Step " + GetStepsPassed() + ">" + "Stop");
            }
                
            GUI.Label(new Rect(10, 50, 190, 30), "Steps: " + GetStepsPassed());
        }

        int ly0 = Runing ? 90 : 130;
        foreach(string s in Logs)
        {
            GUI.Label(new Rect(10, ly0, 190, 60), s);
            ly0 += 40;
        }


    }
    

    void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }
    void OnPostRender()
    {
        GL.PushMatrix();

        CreateLineMaterial();
        lineMaterial.SetPass(0);
        GL.LoadOrtho();

        #region Prepare
        Vector2 d = new Vector2(GridStep / screenRes.x, GridStep / screenRes.y);
        Vector2 gap = d * CellGapK;
        
        #endregion Prepare

        #region Draw Grid
        GL.Begin(GL.LINES);

        GL.Color(subColor);
        //X
        for (float i = GridRectF.y; i <= GridRectF.yMax; i += d.y)
        {
            GL.Vertex3(GridRectF.x, i, 0);
            GL.Vertex3(GridRectF.xMax, i, 0);
        }
        //Y axis lines
        for (float i = GridRectF.x; i <= GridRectF.xMax; i += d.x)
        {
            GL.Vertex3(i, GridRectF.y, 0);
            GL.Vertex3(i, GridRectF.yMax, 0);
        }
        
        GL.End();
        #endregion Draw Grid

        #region Draw Cells
        GL.Begin(GL.QUADS);
        GL.Color(cellColor);
        foreach (Vector2Int c in Cells)
        {
            DrawBox(c, d, gap);

            //Vector2 p0 = (c+OffsetF) * d + gap;
            //if (p0.x >= Grid_x0 && p0.x <= Grid_x1 && p0.y >= Grid_y0 && p0.y <= Grid_y1)
            //    DrawBox(p0, p0+d-gap-gap);
        }
        if (StartCell != null)
        {
            GL.Color(StartCellColor);
            DrawBox(StartCell.Value, d, gap);
            //DrawBox(StartCell.Value + OffsetF, d, gap);
        }
        if (FinishCell!=null)
        {
            GL.Color(FinishCellColor);
            DrawBox(FinishCell.Value, d, gap);

            //DrawBox(FinishCell.Value + OffsetF, d, gap);
        }
        
        GL.End();
        #endregion Draw Cells

        #region Draw Snails
        if (Runing)
        {
            for (int i = 0; i < Snails.Length; i++)
                DrawSnail(SnailPositions[i],SnailDirTempl[SnailDirections[i]], d, Snails[i].GetColor());
        }
        #endregion Draw Snails

        GL.PopMatrix();
    }
    void DrawBox(Vector2 pos, Vector2 d, Vector2 gap)
    {
        Vector2 p0 = GridRectF.min + (pos+Offset) * d + gap;
        Vector2 p1 = p0 + d - 2 * gap;
        if (GridRectF.Contains(p0) && GridRectF.Contains(p1))
        {
            GL.Vertex3(p0.x, p0.y, 0);
            GL.Vertex3(p1.x, p0.y, 0);
            GL.Vertex3(p1.x, p1.y, 0);
            GL.Vertex3(p0.x, p1.y, 0);
        }   
    }
    void DrawRectangle(Vector2 center, Vector2 size)
    {
        if (GridRectF.Contains(center))
        {
            GL.Vertex3(center.x - size.x, center.y - size.y, 0);
            GL.Vertex3(center.x + size.x, center.y - size.y, 0);
            GL.Vertex3(center.x + size.x, center.y + size.y, 0);
            GL.Vertex3(center.x - size.x, center.y + size.y, 0);
        }
    }
    void DrawSnail(Vector2Int SPos, Vector2Int VDir, Vector2 d, Color snailColor)
    {
        Vector2 gap = d*0.2f;
        Vector2 p0 = (SPos+Offset) * d+ GridRectF.min;
        Vector2 c0 = p0 + d / 2f;

        GL.Begin(GL.QUADS);
        GL.Color(snailColor);

        //Body
        DrawRectangle(c0, d/2f - gap);
        //Center Sensor
        DrawRectangle(c0+d*VDir, d/2f - 2f * gap);
        //Left Sensor
        DrawRectangle(c0 + d * (new Vector2Int(-VDir.y,VDir.x)), d / 2f - 2f * gap);
        //Right Sensor
        DrawRectangle(c0 + d * (new Vector2Int(VDir.y, -VDir.x)), d / 2f - 2f * gap);

        DrawRectangle(c0, new Vector2(gap.x/4+d.x*VDir.y, gap.y/4+ d.y*VDir.x));
        DrawRectangle(c0 + new Vector2(d.x * VDir.x / 2, d.y * VDir.y / 2), new Vector2(gap.x / 4 + d.x * VDir.x/2, gap.y / 4 + d.y * VDir.y/2));

        GL.End();
    }
    #endregion Graphics

    #region LoadSave
    public void SaveCells(string SetName)
    {
        if (StartCell!=null && FinishCell != null)
        {
            string str = "";
            str += StartCell.Value.x.ToString() + "," + StartCell.Value.y + ",";
            str += FinishCell.Value.x.ToString() + "," + FinishCell.Value.y + ",";
            foreach (Vector2Int ci in Cells)
                if (ci != FinishCell.Value)
                    str += ci.x.ToString() + "," + ci.y.ToString() + ","; 
            if (str.Length > 0)
                str = str.Substring(0, str.Length - 1);
            PlayerPrefs.SetString("Cells_" + SetName, str);
        }
    }
    public void LoadCells(string SetName)
    {
        Cells.Clear();
        string str = PlayerPrefs.GetString("Cells_" + SetName);
        if (str != null)
        {
            string[] cellWords = str.Split(',');
            int x, y;
            if (int.TryParse(cellWords[0], out x) && int.TryParse(cellWords[1], out y))
                StartCell = new Vector2Int(x, y);
            if (int.TryParse(cellWords[2], out x) && int.TryParse(cellWords[3], out y))
                FinishCell = new Vector2Int(x, y);

            for (int i = 4; i < cellWords.Length - 1; i += 2)
            {
                if (int.TryParse(cellWords[i], out x) && int.TryParse(cellWords[i + 1], out y))
                    Cells.Add(new Vector2Int(x, y));
            }
            Cells.Add(FinishCell.Value);
        }
    }
    #endregion LoadSave
}
