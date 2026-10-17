using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using XamlPrintDocument=Windows.UI.Xaml.Printing.PrintDocument;
using XamlGrid=Windows.UI.Xaml.Controls.Grid;
using XamlImage=Windows.UI.Xaml.Controls.Image;

namespace FontPocket {
    [ComImport,Guid("c5435a42-8d43-4e7b-a68a-ef311e392087"),InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    interface IDesktopPrintManager {
        [return:MarshalAs(UnmanagedType.IInspectable)] object GetForWindow(IntPtr window,ref Guid iid);
        [return:MarshalAs(UnmanagedType.IInspectable)] object ShowPrintUIForWindowAsync(IntPtr window,ref Guid iid);
    }
    [ComImport,Guid("905a0fef-bc53-11df-8c49-001e4fc686da"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IBufferByteAccess {void Buffer(out IntPtr bytes);}
    public sealed class NativeReportPage {
        internal ReportPaginator Position; internal Rectangle Bounds;
        public Windows.Foundation.Size Size {get;internal set;}
        internal XamlGrid Element;internal XamlImage Image;internal long Bytes;
        internal void ReleaseImage() {if(Image!=null) Image.Source=null;if(Bytes>0) GC.RemoveMemoryPressure(Bytes);Bytes=0;}
    }
    public static class NativeReportLayout {
        public static List<NativeReportPage> Paginate(List<ManagedFont> fonts,int mode,Windows.Foundation.Size pageSize,Windows.Foundation.Rect imageable) {
            if(pageSize.Width<144 || pageSize.Height<144 || pageSize.Width>10000 || pageSize.Height>10000) throw new ArgumentException("지원하지 않는 용지 크기입니다.");
            int left=(int)Math.Ceiling(Math.Max(48,imageable.X)),top=(int)Math.Ceiling(Math.Max(48,imageable.Y));
            int right=(int)Math.Floor(Math.Min(pageSize.Width-48,imageable.X+imageable.Width));
            int bottom=(int)Math.Floor(Math.Min(pageSize.Height-48,imageable.Y+imageable.Height));
            var bounds=Rectangle.FromLTRB(left,top,right,bottom);var result=new List<NativeReportPage>();var paginator=new ReportPaginator(fonts,mode);
            using(var bitmap=new Bitmap(1,1)) {bitmap.SetResolution(96,96);using(var graphics=Graphics.FromImage(bitmap)) {
                bool more;do {if(result.Count>=300) throw new InvalidOperationException("한 번에 300쪽까지 인쇄할 수 있습니다. 폰트를 나누어 선택하세요.");
                    var page=new NativeReportPage {Position=paginator.Copy(),Bounds=bounds,Size=pageSize};
                    more=paginator.Render(graphics,bounds);result.Add(page);
                }while(more);
            }}return result;
        }
        public static Bitmap Render(NativeReportPage page) {
            const float scale=1.5f;var bitmap=new Bitmap((int)Math.Ceiling(page.Size.Width*scale),(int)Math.Ceiling(page.Size.Height*scale),PixelFormat.Format32bppPArgb);
            try {bitmap.SetResolution(96,96);using(var graphics=Graphics.FromImage(bitmap)) {graphics.Clear(Color.White);graphics.ScaleTransform(scale,scale);graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;page.Position.Copy().Render(graphics,page.Bounds);}return bitmap;}catch {bitmap.Dispose();throw;}
        }
    }
    public sealed class NativePrintSession : IDisposable {
        static Windows.UI.Xaml.Hosting.WindowsXamlManager xamlManager;
        static readonly Dictionary<IntPtr,NativePrintSession> sessions=new Dictionary<IntPtr,NativePrintSession>();
        readonly Control owner;readonly List<ManagedFont> fonts;readonly int mode;
        readonly PrintManager manager;readonly XamlPrintDocument document;readonly IPrintDocumentSource source;
        readonly IDesktopPrintManager interop;readonly Action<string> report;
        List<NativeReportPage> pages=new List<NativeReportPage>();readonly LinkedList<NativeReportPage> cache=new LinkedList<NativeReportPage>();
        bool disposed;string lastError;PrintTask task;
        NativePrintSession(Control owner,List<ManagedFont> fonts,int mode,Action<string> report) {
            this.owner=owner;this.fonts=fonts.ToList();this.mode=mode;this.report=report??(s=>{});
            this.report("Windows print initialization");
            if(xamlManager==null) {
                xamlManager=Windows.UI.Xaml.Hosting.WindowsXamlManager.InitializeForCurrentThread();
                Application.ApplicationExit+=delegate {if(xamlManager!=null) {xamlManager.Dispose();xamlManager=null;}};
            }
            interop=(IDesktopPrintManager)WindowsRuntimeMarshal.GetActivationFactory(typeof(PrintManager));
            this.report("Windows print activation factory");
            Guid managerId=new Guid("ff2a9694-8c99-44fd-ae4a-19d9aa9a0f0a");manager=(PrintManager)interop.GetForWindow(owner.Handle,ref managerId);
            this.report("Windows PrintManager obtained");
            document=new XamlPrintDocument();source=document.DocumentSource;
            document.Paginate+=Paginate;document.GetPreviewPage+=Preview;document.AddPages+=AddPages;
            manager.PrintTaskRequested+=Requested;owner.Disposed+=OwnerDisposed;
        }
        public static async Task Show(Control owner,List<ManagedFont> fonts,int mode,Action<string> report=null) {
            if(report!=null) report("Windows print API requested");
            if(!PrintManager.IsSupported()) throw new NotSupportedException("이 Windows 환경에서는 기본 인쇄 창을 사용할 수 없습니다.");
            NativePrintSession session;
            if(!sessions.TryGetValue(owner.Handle,out session)) {session=new NativePrintSession(owner,fonts,mode,report);sessions.Add(owner.Handle,session);}
            try {
                Guid asyncId=new Guid("cdb5efb3-5788-509d-9be1-71ccb8a3362a");
                var operation=(IAsyncOperation<bool>)session.interop.ShowPrintUIForWindowAsync(owner.Handle,ref asyncId);
                var completion=new TaskCompletionSource<bool>();
                operation.Completed=delegate(IAsyncOperation<bool> result,AsyncStatus status) {
                    try {if(status==AsyncStatus.Canceled) completion.TrySetCanceled();else if(status==AsyncStatus.Error) completion.TrySetException(result.ErrorCode);else completion.TrySetResult(result.GetResults());}catch(Exception ex) {completion.TrySetException(ex);}
                };
                if(!await completion.Task) session.Dispose();
            }catch {session.Dispose();throw;}
        }
        void OwnerDisposed(object sender,EventArgs e) {Dispose();}
        void Requested(PrintManager sender,PrintTaskRequestedEventArgs args) {
            task=args.Request.CreatePrintTask("Font Pocket · 폰트 견본",request=>request.SetSource(source));
            task.Completed+=Completed;report("Windows PrintTask created");
        }
        void Completed(PrintTask sender,PrintTaskCompletedEventArgs args) {
            report("Windows PrintTask completion: "+args.Completion);
            Post(delegate {if(args.Completion==PrintTaskCompletion.Failed && lastError==null) LocalizedMessage.Show(owner,"인쇄를 완료하지 못했습니다. 프린터 상태를 확인하고 다시 시도하세요.","인쇄",MessageBoxButtons.OK,MessageBoxIcon.Warning);Dispose();});
        }
        void Post(Action action) {if(!owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke(action);}
        void Fail(Exception error) {
            report("Windows printing error: "+error);lastError=error.Message;
            Post(delegate {LocalizedMessage.Show(owner,error.Message,"인쇄",MessageBoxButtons.OK,MessageBoxIcon.Warning);});
        }
        void Paginate(object sender,Windows.UI.Xaml.Printing.PaginateEventArgs args) {
            try {
                var options=(PrintTaskOptions)args.PrintTaskOptions;var description=options.GetPageDescription(0);
                ClearPages();pages=NativeReportLayout.Paginate(fonts,mode,description.PageSize,description.ImageableRect);
                foreach(var page in pages) {
                    page.Image=new XamlImage {Width=page.Size.Width,Height=page.Size.Height,Stretch=Stretch.Fill};
                    page.Element=new XamlGrid {Width=page.Size.Width,Height=page.Size.Height,Background=new SolidColorBrush(Windows.UI.Colors.White)};
                    page.Element.Children.Add(page.Image);
                }
                document.SetPreviewPageCount(pages.Count,Windows.UI.Xaml.Printing.PreviewPageCountType.Final);
                report("Windows native pagination: "+pages.Count+" pages, "+description.PageSize);
            }catch(Exception ex) {ClearPages();document.SetPreviewPageCount(0,Windows.UI.Xaml.Printing.PreviewPageCountType.Final);Fail(ex);}
        }
        void LoadPage(NativeReportPage page,bool allPages) {
            if(page.Image.Source==null) {
                using(var bitmap=NativeReportLayout.Render(page)) {
                    var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                    try {
                        var writable=new WriteableBitmap(bitmap.Width,bitmap.Height);var row=new byte[bitmap.Width*4];
                        IntPtr target;((IBufferByteAccess)(object)writable.PixelBuffer).Buffer(out target);
                        for(int y=0;y<bitmap.Height;y++) {Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);Marshal.Copy(row,0,IntPtr.Add(target,y*row.Length),row.Length);}
                        writable.Invalidate();page.Image.Source=writable;page.Bytes=(long)bitmap.Width*bitmap.Height*4;GC.AddMemoryPressure(page.Bytes);
                    }finally {bitmap.UnlockBits(data);}
                }
            }
            if(!allPages) {
                cache.Remove(page);cache.AddLast(page);
                while(cache.Count>4) {var oldest=cache.First.Value;cache.RemoveFirst();oldest.ReleaseImage();}
            }
            page.Element.Measure(page.Size);page.Element.Arrange(new Windows.Foundation.Rect(0,0,page.Size.Width,page.Size.Height));page.Element.UpdateLayout();
        }
        void Preview(object sender,Windows.UI.Xaml.Printing.GetPreviewPageEventArgs args) {
            try {if(args.PageNumber<1 || args.PageNumber>pages.Count) return;var page=pages[args.PageNumber-1];LoadPage(page,false);document.SetPreviewPage(args.PageNumber,page.Element);report("Windows SetPreviewPage: "+args.PageNumber);}catch(Exception ex) {Fail(ex);}
        }
        void AddPages(object sender,Windows.UI.Xaml.Printing.AddPagesEventArgs args) {
            try {
                // Keep every submitted page alive until the Windows task completes.
                long bytes=pages.Sum(p=>(long)Math.Ceiling(p.Size.Width*1.5)* (long)Math.Ceiling(p.Size.Height*1.5)*4);
                if(bytes>512L*1024*1024) throw new InvalidOperationException("인쇄할 페이지가 너무 많습니다. 폰트를 나누어 선택해 주세요.");
                foreach(var page in pages) LoadPage(page,true);
                foreach(var page in pages) document.AddPage(page.Element);
                document.AddPagesComplete();report("Windows AddPagesComplete: "+pages.Count);
            }catch(Exception ex) {Fail(ex);document.AddPagesComplete();}
        }
        void ClearPages() {foreach(var page in pages) page.ReleaseImage();pages.Clear();cache.Clear();}
        public void Dispose() {
            if(disposed) return;disposed=true;owner.Disposed-=OwnerDisposed;
            manager.PrintTaskRequested-=Requested;document.Paginate-=Paginate;document.GetPreviewPage-=Preview;document.AddPages-=AddPages;
            if(task!=null) task.Completed-=Completed;ClearPages();
            var key=sessions.FirstOrDefault(p=>p.Value==this);if(key.Value!=null) sessions.Remove(key.Key);
        }
    }
}
