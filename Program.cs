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
    [STAThread] static void Main(){using var m=new Mutex(true,"BaguaLiveWallpaper.SingleInstance",out bool first);if(!first)return;ApplicationConfiguration.Initialize();try{Application.Run(new Wallpaper());}catch(Exception ex){Log(ex);}}
    static void Log(Exception ex){try{string d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BaguaLiveWallpaper");Directory.CreateDirectory(d);File.AppendAllText(Path.Combine(d,"error.log"),ex+Environment.NewLine);}catch{}}
}
internal sealed class Wallpaper:Form
{
 const int GWL_STYLE=-16,WM_NCHITTEST=0x84,WM_HOTKEY=0x312,HTTRANSPARENT=-1,HOTKEY_ID=0x4247;const long WS_CHILD=0x40000000L;const uint MOD_ALT=1,MOD_CONTROL=2,SWP_NOACTIVATE=0x10,SWP_SHOWWINDOW=0x40,WM_SPAWN_WORKER=0x052C;const double TURN=24;
 readonly System.Windows.Forms.Timer timer;readonly Stopwatch clock=Stopwatch.StartNew();
 static readonly float[] R={58,82,112,145,185,230,278,330,385,440};
 static readonly string[] G={"乾","兑","离","震","巽","坎","艮","坤"},A={"甲","乙","丙","丁","戊","己","庚","辛","壬","癸"},B={"子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥"},E={"木","火","土","金","水","天","地","人"},H={"乾为天","坤为地","水雷屯","山水蒙","水天需","天水讼","地水师","水地比","风天小畜","天泽履","地天泰","天地否","天火同人","火天大有","地山谦","雷地豫","泽雷随","山风蛊","地泽临","风地观","火雷噬嗑","山火贲","山地剥","地雷复","天雷无妄","山天大畜","山雷颐","泽风大过","坎为水","离为火","泽山咸","雷风恒"},T={"立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至","小寒","大寒"};
 public Wallpaper(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=SystemInformation.VirtualScreen.Location;ClientSize=SystemInformation.VirtualScreen.Size;AutoScaleMode=AutoScaleMode.None;BackColor=Color.Black;DoubleBuffered=true;TopMost=false;timer=new System.Windows.Forms.Timer{Interval=16};timer.Tick+=(_,_)=>Invalidate();Shown+=(_,_)=>Attach();FormClosed+=(_,_)=>{timer.Stop();if(IsHandleCreated)Native.UnregisterHotKey(Handle,HOTKEY_ID);};}
 protected override void OnShown(EventArgs e){base.OnShown(e);timer.Start();}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.Black);g.SmoothingMode=SmoothingMode.AntiAlias;g.CompositingQuality=CompositingQuality.HighQuality;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;float s=Math.Min(ClientSize.Width/1920f,ClientSize.Height/1080f),cx=ClientSize.Width/2f,cy=ClientSize.Height/2f;double a=clock.Elapsed.TotalSeconds/TURN*360;g.TranslateTransform(cx,cy);using var p=new Pen(Color.White,Math.Max(.85f,1.25f*s));using var bp=new Pen(Color.White,Math.Max(1.5f,2f*s));
  // Every annular band is a separate rotating object. The center yin-yang is fixed.
  // Nine bands alternate direction and have slightly different speeds so relative motion is obvious.
  double[] phase={0,9,18,27,36,45,54,63,72};
  double[] speed={1.00,.86,1.08,.92,1.03,.89,1.12,.95,1.06};
  string[][] labels={G,A,B,E,H,T,G,A,B};
  for(int i=0;i<9;i++){
      double dir=(i%2==0)?1:-1;
      double angle=a*speed[i]*dir+phase[i];
      Band(g,R[i],R[i+1],angle,i,s,p,bp,labels[i],i%3==0?A:(i%3==1?H:T));
  }
  FixedCenter(g,s,bp);g.ResetTransform();}
 static void Band(Graphics g,float inner,float outer,double angle,int layer,float s,Pen p,Pen bp,string[] labels,string[] alt){var save=g.Save();g.RotateTransform((float)angle);g.DrawEllipse(bp,-inner*s,-inner*s,2*inner*s,2*inner*s);g.DrawEllipse(bp,-outer*s,-outer*s,2*outer*s,2*outer*s);int n=layer<2?12:layer<5?20:layer<7?28:36;float mid=(inner+outer)*.5f*s;float fs=layer<2?11.5f:layer<5?11f:10.5f;using var f=new Font("Microsoft YaHei UI",Math.Max(7.5f,fs*s),FontStyle.Regular,GraphicsUnit.Pixel);using var br=new SolidBrush(Color.White);for(int i=0;i<n;i++){double q=-Math.PI/2+i*2*Math.PI/n;float r1=inner*s,r2=outer*s;g.DrawLine(p,(float)Math.Cos(q)*r1,(float)Math.Sin(q)*r1,(float)Math.Cos(q)*r2,(float)Math.Sin(q)*r2);string text=i%4==0?alt[i%alt.Length]:labels[i%labels.Length];float x=(float)Math.Cos(q)*mid,y=(float)Math.Sin(q)*mid;var st=g.Save();g.TranslateTransform(x,y);float deg=(float)(q*180/Math.PI+90);if(deg>90&&deg<270)deg+=180;g.RotateTransform(deg);SizeF z=g.MeasureString(text,f);g.DrawString(text,f,br,-z.Width/2,-z.Height/2);g.Restore(st);}g.Restore(save);}
 static void FixedCenter(Graphics g,float s,Pen o){float r=58*s;using var w=new SolidBrush(Color.White);using var b=new SolidBrush(Color.Black);g.FillEllipse(b,-r,-r,2*r,2*r);g.DrawEllipse(o,-r,-r,2*r,2*r);g.FillPie(w,-r,-r,2*r,2*r,90,180);g.FillPie(b,-r,-r,2*r,2*r,270,180);g.FillEllipse(w,-r/2,-r,r,r);g.FillEllipse(b,-r/2,0,r,r);float d=r*.22f;g.FillEllipse(b,-d/2,-r*.55f,d,d);g.FillEllipse(w,-d/2,r*.33f,d,d);}
 protected override void WndProc(ref Message m){if(m.Msg==WM_NCHITTEST){m.Result=(IntPtr)HTTRANSPARENT;return;}if(m.Msg==WM_HOTKEY&&m.WParam.ToInt32()==HOTKEY_ID){Close();return;}base.WndProc(ref m);}
 void Attach(){IntPtr prog=Native.FindWindow("Progman",null);if(prog==IntPtr.Zero)return;Native.SendMessageTimeout(prog,WM_SPAWN_WORKER,IntPtr.Zero,IntPtr.Zero,0,1000,out _);IntPtr worker=IntPtr.Zero;Native.EnumWindows((h,_)=>{if(Native.FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){worker=Native.FindWindowEx(IntPtr.Zero,h,"WorkerW",null);return false;}return true;},IntPtr.Zero);if(worker==IntPtr.Zero)worker=prog;IntPtr style=Native.GetWindowLongPtr(Handle,GWL_STYLE);Native.SetWindowLongPtr(Handle,GWL_STYLE,(IntPtr)(style.ToInt64()|WS_CHILD));Native.SetParent(Handle,worker);Native.SetWindowPos(Handle,IntPtr.Zero,0,0,ClientSize.Width,ClientSize.Height,SWP_NOACTIVATE|SWP_SHOWWINDOW);Native.RegisterHotKey(Handle,HOTKEY_ID,MOD_CONTROL|MOD_ALT,(uint)Keys.Q);}
 static class Native{public delegate bool EnumWindowsProc(IntPtr hWnd,IntPtr lParam);[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string? c,string? n);[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string? c,string? n);[DllImport("user32.dll")]public static extern bool EnumWindows(EnumWindowsProc cb,IntPtr lp);[DllImport("user32.dll")]public static extern IntPtr SetParent(IntPtr c,IntPtr p);[DllImport("user32.dll")]public static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint v);[DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id);[DllImport("user32.dll")]public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out IntPtr r);[DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]public static extern IntPtr GetWindowLongPtr(IntPtr h,int i);[DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]public static extern IntPtr SetWindowLongPtr(IntPtr h,int i,IntPtr v);[DllImport("user32.dll",SetLastError=true)]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);}
}