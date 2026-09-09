using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
 [STAThread] static void Main(){using var m=new Mutex(true,"BaguaLiveWallpaper.SingleInstance",out bool first);if(!first)return;ApplicationConfiguration.Initialize();try{Application.Run(new Wallpaper());}catch(Exception e){Log(e);}}
 static void Log(Exception e){try{var d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BaguaLiveWallpaper");Directory.CreateDirectory(d);File.AppendAllText(Path.Combine(d,"error.log"),e+"\r\n");}catch{}}
}

internal sealed class Wallpaper:Form
{
 const int GWL_STYLE=-16,WM_NCHITTEST=0x84,WM_ERASEBKGND=0x14,WM_HOTKEY=0x312,HTTRANSPARENT=-1,HOTKEY=0x4247;
 const long WS_CHILD=0x40000000L;const uint MOD_ALT=1,MOD_CONTROL=2,NOACT=0x10,SHOW=0x40;static readonly IntPtr BOTTOM=new(1);
 readonly System.Windows.Forms.Timer t;readonly Stopwatch sw=Stopwatch.StartNew();const double turn=36;
 static readonly bool[][] trig={new[]{true,true,true},new[]{false,false,false},new[]{false,true,false},new[]{true,false,true},new[]{false,false,true},new[]{true,false,false},new[]{true,true,false},new[]{false,true,true}};
 static readonly string[] names={"乾","坤","坎","离","震","巽","艮","兑"};
 public Wallpaper(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=SystemInformation.VirtualScreen.Location;ClientSize=SystemInformation.VirtualScreen.Size;BackColor=Color.Black;DoubleBuffered=true;TopMost=false;t=new(){Interval=16};t.Tick+=(_,_)=>Invalidate();Shown+=(_,_)=>Attach();FormClosed+=(_,_)=>{t.Stop();if(IsHandleCreated)N.UnregisterHotKey(Handle,HOTKEY);};}
 protected override void OnPaintBackground(PaintEventArgs e)=>e.Graphics.Clear(Color.Black);
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.Black);g.SmoothingMode=SmoothingMode.AntiAlias;g.CompositingQuality=CompositingQuality.HighQuality;g.PixelOffsetMode=PixelOffsetMode.HighQuality;float s=Math.Min(ClientSize.Width/1920f,ClientSize.Height/1080f),cx=ClientSize.Width/2f,cy=ClientSize.Height/2f,r=330*s;g.TranslateTransform(cx,cy);g.RotateTransform((float)(sw.Elapsed.TotalSeconds/turn*360));using var p=new Pen(Color.White,Math.Max(1.2f,r/250));using var q=new Pen(Color.White,Math.Max(2f,r/155));for(int i=1;i<=10;i++){float a=i/10f;g.DrawEllipse(p,-r*a,-r*a,2*r*a,2*r*a);}for(int n=8;n<=64;n*=2)Radial(g,r,n,(n==8?.13f:n==16?.23f:n==32?.33f:.54f),n==8?.23f:n==16?.33f:n==32?.54f:1,p);for(int i=0;i<8;i++){double a=-Math.PI/2+i*Math.PI/4;var st=g.Save();g.TranslateTransform((float)(Math.Cos(a)*r*.69),(float)(Math.Sin(a)*r*.69));g.RotateTransform((float)(a*180/Math.PI+90));Tri(g,trig[i],r*.12f,Math.Max(2.5f,r*.018f));using var f=new Font("Microsoft YaHei UI",Math.Max(8,r*.032f),FontStyle.Bold,GraphicsUnit.Pixel);using var b=new SolidBrush(Color.White);g.DrawString(names[i],f,b,new RectangleF(-r*.025f,r*.14f,r*.05f,r*.05f));g.Restore(st);}Yin(g,r*.15f,q);g.ResetTransform();}
 static void Radial(Graphics g,float r,int n,float a,float b,Pen p){for(int i=0;i<n;i++){double x=i*Math.PI*2/n;g.DrawLine(p,(float)(Math.Cos(x)*r*a),(float)(Math.Sin(x)*r*a),(float)(Math.Cos(x)*r*b),(float)(Math.Sin(x)*r*b));}}
 static void Tri(Graphics g,bool[] a,float w,float h){using var b=new SolidBrush(Color.White);for(int i=0;i<3;i++){float y=(i-1)*h*2.3f;if(a[i])g.FillRectangle(b,-w/2,y-h/2,w,h);else{float z=w*.42f;g.FillRectangle(b,-w/2,y-h/2,z,h);g.FillRectangle(b,w/2-z,y-h/2,z,h);}}}
 static void Yin(Graphics g,float r,Pen p){using var w=new SolidBrush(Color.White);using var k=new SolidBrush(Color.Black);float d=r*2;g.FillEllipse(w,-r,-r,d,d);g.FillPie(k,-r,-r,d,d,-90,180);g.FillEllipse(k,-r/2,-r,r,r);g.FillEllipse(w,0,0,r,r);g.FillEllipse(w,-r*.14f,-r*.64f,r*.28f,r*.28f);g.FillEllipse(k,-r*.14f,r*.36f,r*.28f,r*.28f);g.DrawEllipse(p,-r,-r,d,d);}
 void Attach(){try{IntPtr p=N.FindWindow("Progman",null);if(p==IntPtr.Zero)throw new Exception("Progman not found");N.SendMessageTimeout(p,0x052C,IntPtr.Zero,IntPtr.Zero,0,1000,out _);IntPtr w=N.FindWorkerW();if(w==IntPtr.Zero)throw new Exception("WorkerW not found");IntPtr h=Handle;long style=N.Get(h,GWL_STYLE).ToInt64();N.Set(h,GWL_STYLE,new IntPtr(style|WS_CHILD));N.SetParent(h,w);var z=SystemInformation.VirtualScreen.Size;N.SetWindowPos(h,BOTTOM,0,0,z.Width,z.Height,NOACT|SHOW);N.ShowWindow(h,5);if(!N.RegisterHotKey(h,HOTKEY,MOD_CONTROL|MOD_ALT,(uint)Keys.Q))throw new Exception("Hotkey failed");t.Start();}catch(Exception e){Program.Log(e);Close();}}
 protected override void WndProc(ref Message m){if(m.Msg==WM_NCHITTEST){m.Result=(IntPtr)HTTRANSPARENT;return;}if(m.Msg==WM_HOTKEY&&m.WParam==(IntPtr)HOTKEY){Close();return;}if(m.Msg==WM_ERASEBKGND){m.Result=(IntPtr)1;return;}base.WndProc(ref m);}
}
internal static class N
{
 public delegate bool E(IntPtr h,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string? c,string? t);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string? c,string? t);
 [DllImport("user32.dll")]public static extern bool EnumWindows(E e,IntPtr l);
 [DllImport("user32.dll")]public static extern IntPtr SetParent(IntPtr c,IntPtr p);
 [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr h,int c);
 [DllImport("user32.dll",SetLastError=true)]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int z,uint f);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr G64(IntPtr h,int i);[DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern IntPtr G32(IntPtr h,int i);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr S64(IntPtr h,int i,IntPtr v);[DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern IntPtr S32(IntPtr h,int i,IntPtr v);
 [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterHotKey(IntPtr h,int i,uint m,uint k);[DllImport("user32.dll",SetLastError=true)]public static extern bool UnregisterHotKey(IntPtr h,int i);
 [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out IntPtr r);
 public static IntPtr Get(IntPtr h,int i)=>IntPtr.Size==8?G64(h,i):G32(h,i);public static IntPtr Set(IntPtr h,int i,IntPtr v)=>IntPtr.Size==8?S64(h,i,v):S32(h,i,v);
 public static IntPtr FindWorker(){IntPtr r=IntPtr.Zero;EnumWindows((top,_)=>{if(FindWindowEx(top,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){r=FindWindowEx(IntPtr.Zero,top,"WorkerW",null);return false;}return true;},IntPtr.Zero);return r;}
}
