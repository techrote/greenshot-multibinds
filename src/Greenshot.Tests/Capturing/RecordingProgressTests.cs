using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Greenshot.Capturing;
using Xunit;
namespace Greenshot.Tests.Capturing
{
 public class RecordingProgressTests
 {
  [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
  [Fact] public void MeasuredFpsUsesCaptureIntervalsAndSavedCountIsSeparate()
  {
   var state=new RecordingProgress(); state.Begin(Stopwatch.StartNew());
   state.Captured(0); state.Captured(40); state.Captured(80); state.Saved();
   state.CaptureStopped(0.1);
   var snapshot=state.Snapshot();
   Assert.Equal(25, snapshot.Fps); Assert.Equal(3,snapshot.Captured); Assert.Equal(1,snapshot.Saved);
   Assert.Equal(TimeSpan.FromMilliseconds(100),snapshot.Elapsed); Assert.True(snapshot.CaptureStopped);
  }
  [Fact] public void OldSamplesExpireAndNoFramesMeansZeroFps()
  {
   var state=new RecordingProgress(); state.Begin(Stopwatch.StartNew());
   Assert.Equal(0,state.Snapshot().Fps);
   state.Captured(0); state.Captured(33); state.CaptureStopped(2);
   Assert.Equal(0,state.Snapshot().Fps);
  }
  [Fact] public async Task FinalProgressMatchesSavedFilesAfterDraining()
  {
   string folder=Path.Combine(Path.GetTempPath(),"GreenshotOverlayTest-"+Guid.NewGuid().ToString("N"));
   var state=new RecordingProgress();
   using var stop=new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
   int count=await PngFrameRecorder.RecordAsync(new Rectangle(0,0,8,8),folder,30,stop.Token,capture:()=>new Bitmap(8,8),progress:state);
   var snapshot=state.Snapshot();
   Assert.True(count>0); Assert.Equal(count,snapshot.Captured); Assert.Equal(count,snapshot.Saved); Assert.True(snapshot.CaptureStopped);
  }
  [Fact] public void OverlayNativeWindowIsExcludedFromCaptureAndDoesNotTakeFocus()
  {
   TestEnvironment.EnsureInitialized(); Exception error=null;
   var thread=new Thread(()=>{
    Window window=null;
    try {
     var type=typeof(RegionRecording).Assembly.GetType("Greenshot.Capturing.RecordingStatusWindow",true);
     window=(Window)Activator.CreateInstance(type,new object[]{new Rectangle(0,0,32,24),new RecordingProgress(),new Func<TimeSpan>(()=>TimeSpan.FromMinutes(10)),"F6"});
     window.Show();
     Assert.True(GetWindowDisplayAffinity(new WindowInteropHelper(window).Handle,out uint affinity));
     Assert.Equal(0x11u,affinity); Assert.False(window.ShowActivated); Assert.False(window.IsHitTestVisible);
    } catch(Exception ex){error=ex;} finally {window?.Close();}
   });
   thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); Assert.Null(error);
  }
 }
}
