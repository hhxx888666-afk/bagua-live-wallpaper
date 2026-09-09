using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { Application.Run(new Wallpaper()); } catch (Exception ex) { Log(ex); }
    }
    private static void Log(Exception ex)
    {
        try { string d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BaguaLiveWallpaper"); Directory.CreateDirectory(d); File.AppendAllText(Path.Combine(d,"error.log"),ex+Environment.NewLine); } catch { }
    }
}

internal sealed class Wallpaper : Form
{
    const int GWL_STYLE=-16, WM_NCHITTEST=0x84, WM_HOTKEY=0x312, HTTRANSPARENT=-1, HOTKEY_ID=0x4247;
    const long WS_CHILD=0x40000000L; const uint MOD_ALT=1, MOD_CONTROL=2, SWP_NOACTIVATE=0x10, SWP_SHOWWINDOW=0x40, WM_SPAWN_WORKER=0x052C;
    const double TurnSeconds=24.0;
    readonly System.Windows.Forms.Timer timer; readonly Stopwatch clock=Stopwatch.StartNew();
    // Four actual rotating bands. Their boundaries are deliberately kept separate so movement is visible.
    static readonly float[] R={58,82,112,145,185,230,278,330,385,440};
    static readonly int[] N={8,8,16,16,32,32,32,64,64};
    static readonly string[] G={"乾","兑","离","震","巽","坎","艮","坤"};
    static readonly string[] A={"甲","乙","丙","丁","戊","己","庚","辛","壬","癸"};
    static readonly string[] B={"子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥"};
    static readonly string[] E={"木","火","土","金","水","天","地","人"};
    static readonly string[] H={"乾为天","坤为地","水雷屯","山水蒙","水天需","天水讼","地水师","水地比","风天小畜","天泽履","地天泰","天地否","天火同人","火天大有","地山谦","雷地豫","泽雷随","山风蛊","地泽临","风地观","火雷噬嗑","山火贲","山地剥","地雷复","天雷无妄","山天大畜","山雷颐","泽风大过","坎为水","离为火","泽山咸","雷风恒"};
    static readonly string[] T={"立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至","小寒","大寒"};

    public Wallpaper()
    {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Location=SystemInformation.VirtualScreen.Location; ClientSize=SystemInformation.VirtualScreen.Size; AutoScaleMode=AutoScaleMode.None; BackColor=Color.Black; DoubleBuffered=true; TopMost=false;
        timer=new System.Windows.Forms.Timer{Interval=16}; timer.Tick+=(_,_)=>Invalidate(); Shown+=(_,_)=>Attach(); FormClosed+=(_,_)=>{timer.Stop();if(IsHandleCreated)Native.UnregisterHotKey(Handle,HOTKEY_ID);};
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);timer.Start();}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics; g.Clear(Color.Black); g.SmoothingMode=SmoothingMode.AntiAlias; g.CompositingQuality=CompositingQuality.HighQuality; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        float s=Math.Min(ClientSize.Width/1920f,ClientSize.Height/1080f); float cx=ClientSize.Width/2f,cy=ClientSize.Height/2f; double baseAngle=clock.Elapsed.TotalSeconds/TurnSeconds*360.0;
        g.TranslateTransform(cx,cy); using var p=new Pen(Color.White,Math.Max(.8f,1.2f*s)); using var bp=new Pen(Color.White,Math.Max(1.4f,1.9f*s));
        // Keep central yin-yang and the four bands independent. Each band is rendered under its own transform.
        DrawBand(g,58,112,baseAngle, 1,s,p,bp,G,A);
        DrawBand(g,112,185,-baseAngle,2,s,p,bp,B,H);
        DrawBand(g,185,278,baseAngle,3,s,p,bp,E,H);
        DrawBand(g,278,440,-baseAngle,4,s,p,bp,T,H);
        DrawFixedCenter(g,s,bp);
        g.ResetTransform();
    }
    static void DrawBand(Graphics g,float inner,float outer,double angle,int layer,float s,Pen p,Pen bp,string[] labels,string[] alt)
    {
        var st=g.Save(); g.RotateTransform((float)angle);
        g.DrawEllipse(bp,-inner*s,-inner*s,2*inner*s,2*inner*s); g.DrawEllipse(bp,-outer*s,-outer*s,2*outer*s,2*outer*s);
        int count=layer==1?16:layer==2?24:layer==3?32:48; float mid=(inner+outer)*.5f*s; float width=(outer-inner)*s;
        using var font=new Font("Microsoft YaHei UI",Math.Max(7f,(layer==4?9.5f:11f)*s),FontStyle.Regular,GraphicsUnit.Pixel); using var br=new SolidBrush(Color.White);
        for(int i=0;i<count;i++){
            double a=-Math.PI/2+i*2*Math.PI/count; float r1=inner*s,r2=outer*s;
            g.DrawLine(p,(float)Math.Cos(a)*r1,(float)Math.Sin(a)*r1,(float)Math.Cos(a)*r2,(float)Math.Sin(a)*r2);
            string text=labels[i%labels.Length]; if(i%5==0) text=alt[i%alt.Length];
            float x=(float)Math.Cos(a)*mid,y=(float)Math.Sin(a)*mid; var ss=g.Save(); g.TranslateTransform(x,y); float deg=(float)(a*180/Math.PI+90); if(deg>90&&deg<270)deg+=180; g.RotateTransform(deg); SizeF z=g.MeasureString(text,font); g.DrawString(text,font,br,-z.Width/2,-z.Height/2); g.Restore(ss);
        }
        g.Restore(st);
    }
    static void DrawFixedCenter(Graphics g,float s,Pen outline)
    {
        float r=58*s; using var white=new SolidBrush(Color.White); using var black=new SolidBrush(Color.Black); g.FillEllipse(black,-r,-r,2*r,2*r); g.DrawEllipse(outline,-r,-r,2*r,2*r);
        g.FillPie(white,-r,-r,2*r,2*r,90,180); g.FillPie(black,-r,-r,2*r,2*r,270,180); g.FillEllipse(white,-r/2,-r,r,r); g.FillEllipse(black,-r/2,0,r,r);
        float d=r*.22f; g.FillEllipse(black,-d/2,-r*.55f,d,d); g.FillEllipse(white,-d/2,r*.33f,d,d);
    }
    protected override void WndProc(ref Message m){if(m.Msg==WM_NCHITTEST){m.Result=(IntPtr)HTTRANSPARENT;return;}if(m.Msg==WM_HOTKEY&&m.WParam.ToInt32()==HOTKEY_ID){Close();return;}base.WndProc(ref m);}
    void Attach(){IntPtr prog=Native.FindWindow("Progman",null);if(prog==IntPtr.Zero)return;Native.SendMessageTimeout(prog,WM_SPAWN_WORKER,IntPtr.Zero,IntPtr.Zero,0,1000,out _);IntPtr worker=IntPtr.Zero;Native.EnumWindows((h,_)=>{IntPtr shell=Native.FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);if(shell!=IntPtr.Zero){worker=Native.FindWindowEx(IntPtr.Zero,h,"WorkerW",null);return false;}return true;},IntPtr.Zero);if(worker==IntPtr.Zero)worker=prog;IntPtr style=Native.GetWindowLongPtr(Handle,GWL_STYLE);Native.SetWindowLongPtr(Handle,GWL_STYLE,(IntPtr)(style.ToInt64()|WS_CHILD));Native.SetParent(Handle,worker);Native.SetWindowPos(Handle,IntPtr.Zero,0,0,ClientSize.Width,ClientSize.Height,SWP_NOACTIVATE|SWP_SHOWWINDOW);Native.RegisterHotKey(Handle,HOTKEY_ID,MOD_CONTROL|MOD_ALT,(uint)Keys.Q);}
    static class Native{
        public delegate bool EnumWindowsProc(IntPtr hWnd,IntPtr lParam);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string? c,string? n); [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string? c,string? n); [DllImport("user32.dll")]public static extern bool EnumWindows(EnumWindowsProc cb,IntPtr lp); [DllImport("user32.dll")]public static extern IntPtr SetParent(IntPtr c,IntPtr p); [DllImport("user32.dll")]public static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint v); [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id); [DllImport("user32.dll")]public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out IntPtr r); [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]public static extern IntPtr GetWindowLongPtr(IntPtr h,int i); [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]public static extern IntPtr SetWindowLongPtr(IntPtr h,int i,IntPtr v); [DllImport("user32.dll",SetLastError=true)]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
    }
}