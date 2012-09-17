using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Kinect;
using Microsoft.DirectX.DirectSound;
using System.Windows.Interop;
using KinectControls;
using System.Globalization;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace KinectDJ
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{

		private KinectSensor sensor;
		private WriteableBitmap bitmap;
		private byte[] bitmappixels;
		private Skeleton[] skeletons;
		private DrawingGroup drawingGroup;
		private DrawingImage drawingImage;
		private Int32Rect updateRect;
		private Rect drawingRect;
		private Device DSDevice;
		private BufferDescription bufferDesc;
		private SecondaryBuffer secondBuffer;
		private EffectDescription[] effectDesc;
		private EffectsParamEq[] settings;
		private int mode;
		private GeometryButton button,muteButton,boostButton,gateButton;
		private JointIntersection intersect;
		private JointDistance distance;
		private HandVolume volume;
		private int prevvolume;
		DispatcherTimer timer;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender,RoutedEventArgs e)
		{
			timer=new DispatcherTimer();
			timer.Tick+=GateTick;
			mode=0;
			DSDevice=new Device();
			DSDevice.SetCooperativeLevel(new WindowInteropHelper(this).Handle,CooperativeLevel.Priority);
			bufferDesc=new BufferDescription();
			bufferDesc.ControlEffects=true;
			bufferDesc.ControlFrequency=true;
			bufferDesc.ControlPan=true;
			bufferDesc.ControlVolume=true;
			drawingGroup=new DrawingGroup();
			drawingImage=new DrawingImage(drawingGroup);
			screen.Source=drawingImage;
			var connectedSensors=(from s in KinectSensor.KinectSensors
								  where s.Status==KinectStatus.Connected select s).ToArray();
			if(connectedSensors.Length==0){
				MessageBox.Show("Kinect is not ready!","KinectTestApp",MessageBoxButton.OK,MessageBoxImage.Error);
				Close();
				return;
			}
			sensor=connectedSensors[0];
			sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
			sensor.SkeletonStream.Enable();
			bitmappixels=new byte[sensor.ColorStream.FramePixelDataLength];
			skeletons=new Skeleton[sensor.SkeletonStream.FrameSkeletonArrayLength];
			bitmap=new WriteableBitmap(sensor.ColorStream.FrameWidth,sensor.ColorStream.FrameHeight,96.0,96.0,PixelFormats.Bgr32,null);
			updateRect=new Int32Rect(0,0,sensor.ColorStream.FrameWidth,sensor.ColorStream.FrameHeight);
			drawingRect=new Rect(0.0,0.0,sensor.ColorStream.FrameWidth,sensor.ColorStream.FrameHeight);
			sensor.AllFramesReady+=AllFramesReady;
			drawingGroup.ClipGeometry=new RectangleGeometry(drawingRect);
			button=new GeometryButton(sensor,new RectangleGeometry(new Rect(0,0,60,60)),new []{JointType.HandLeft,JointType.HandRight});
			muteButton=new GeometryButton(sensor,new RectangleGeometry(new Rect(440,0,200,100)),new []{JointType.HandRight,JointType.HandLeft});
			boostButton=new GeometryButton(sensor,new RectangleGeometry(new Rect(580,150,60,480-300)),new []{JointType.HandLeft,JointType.HandRight});
			gateButton=new GeometryButton(sensor,new RectangleGeometry(new Rect(0,150,60,480-300)),new []{JointType.HandLeft,JointType.HandRight});
			intersect=new JointIntersection(sensor,JointType.HandRight,new []{JointType.KneeRight});
			distance=new JointDistance(sensor,JointType.HandRight,JointType.HandLeft);
			volume=new HandVolume(sensor);
			button.JointHitting+=Ring;
			muteButton.JointLeave+=DisableMute;
			//boostButton.JointHitting+=EnableBoost;
			//boostButton.JointHitting+=DisableBoost;
			gateButton.JointHitting+=EnableGate;
			gateButton.JointLeave+=DisableGate;
			intersect.JointIntersect+=ChangeMode;
			try{
				sensor.Start();
			}catch(IOException){
				MessageBox.Show("Error detected!","KinectTestApp",MessageBoxButton.OK,MessageBoxImage.Error);
				Close();
			}
			return;
		}

		private void AllFramesReady(object sender,AllFramesReadyEventArgs e)
		{
			if(secondBuffer!=null){
				using(ColorImageFrame colorImage=e.OpenColorImageFrame())
					using(SkeletonFrame skeletonFrame=e.OpenSkeletonFrame())
						if(colorImage!=null&&skeletonFrame!=null){
							skeletonFrame.CopySkeletonDataTo(skeletons);
							//colorImage.CopyPixelDataTo(bitmappixels);
							//bitmap.WritePixels(updateRect,bitmappixels,bitmap.PixelWidth*sizeof(int),0);
							using(DrawingContext drawingContext=drawingGroup.Open()){
								//drawingContext.DrawImage(bitmap,drawingRect);
								drawingContext.DrawRectangle(Brushes.Black,null,drawingRect);
								var redPen=new Pen(Brushes.Red,5.0);
								drawingContext.DrawGeometry(button.IsHitting?Brushes.White:null,redPen,button.Geometry);
								drawingContext.DrawGeometry(muteButton.IsHitting?Brushes.White:null,redPen,muteButton.Geometry);
								//drawingContext.DrawGeometry(boostButton.IsHitting?Brushes.White:null,redPen,boostButton.Geometry);
								drawingContext.DrawGeometry(gateButton.IsHitting?Brushes.White:null,redPen,gateButton.Geometry);
								drawingContext.DrawText(new FormattedText(secondBuffer.Status.Playing?"■":"▶",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("メイリオ"),44,Brushes.Red),new Point(0,0));
								foreach(Skeleton skel in skeletons){
									if(skel.TrackingState==SkeletonTrackingState.Tracked){
										foreach(Joint joint in skel.Joints){
											if(joint.TrackingState==JointTrackingState.Tracked){
												var depthPoint=sensor.MapSkeletonPointToDepth(joint.Position,DepthImageFormat.Resolution640x480Fps30);
												drawingContext.DrawEllipse(Brushes.Green,null,new Point(depthPoint.X,depthPoint.Y),10,10);
											}
										}
										if(mode==1){
											drawingContext.DrawLine(new Pen(Brushes.DarkBlue,20),distance.Joint1Location,distance.Joint2Location);
											var volume=-10000+(distance.Distance<=250?distance.Distance:250)*35;
											if(timer.IsEnabled) prevvolume=volume;
											else secondBuffer.Volume=volume;
										}else if(mode==2){
											var pen=new Pen(new SolidColorBrush(Color.FromArgb(0x7F,0,0,0x8B)),12.5);
											drawingContext.DrawLine(pen,volume.MiddlePoint,volume.RightHandLocation);
											var mat=Matrix.Identity;
											mat.RotateAt(volume.Angle,volume.MiddlePoint.X,volume.MiddlePoint.Y);
											drawingContext.DrawLine(pen,volume.MiddlePoint,mat.Transform(volume.RightHandLocation));
											settings[0].Gain=15/180*(-volume.Angle);
											settings[1].Gain=10/180*(-volume.Angle);
											settings[2].Gain=15/180*(volume.Angle);
											settings[3].Gain=15/180*(volume.Angle);
											settings[4].Gain=15/180*(volume.Angle);
											for(int i=0;i<settings.Length;i++){
												var effectInst=(ParamEqEffect)secondBuffer.GetEffects(i);
												effectInst.AllParameters=settings[i];
											}
										}
										drawingContext.DrawText(new FormattedText("ControlMode:"+(mode==0?"None":mode==1?"Volume":"Filter"),CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("メイリオ"),40,Brushes.White),new Point(0,400));
										if(muteButton.IsHitting){
											secondBuffer.Volume=-10000;
										}
										break;
									}
								}
							}
						}
			}
			return;
		}

		private void Window_Closing(object sender,System.ComponentModel.CancelEventArgs e)
		{
			if(sensor!=null) sensor.Stop();
			return;
		}

		private void Ring(object sender,JointHittingEventArgs e)
		{
			if(!secondBuffer.Status.Playing){
				secondBuffer.Play(0,BufferPlayFlags.Default);
				secondBuffer.Volume=0;
			}else secondBuffer.Stop();
			return;
		}

		private void ChangeMode(object sender,JointIntersectEventArgs e)
		{
			mode=mode==1?0:++mode;
			return;
		}

		private void EnableMute(object sender,JointHittingEventArgs e)
		{
			prevvolume=secondBuffer.Volume;
			return;
		}

		private void DisableMute(object sender,EventArgs e)
		{
			secondBuffer.Volume=prevvolume;
			return;
		}

		private void EnableBoost(object sender,JointHittingEventArgs e)
		{
			prevvolume=secondBuffer.Volume;
			secondBuffer.Volume=0;
			return;
		}

		private void DisableBoost(object sender,EventArgs e)
		{
			secondBuffer.Volume=prevvolume;
			return;
		}

		private void Window_KeyDown(object sender,KeyEventArgs e)
		{
			if(e.Key==Key.Escape) Close();
			else if(e.Key==Key.S){
				if(WindowState==WindowState.Normal) WindowState=WindowState.Maximized;
				else WindowState=WindowState.Normal;
			}
		}

		private void screen_DragEnter(object sender,DragEventArgs e)
		{
			if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects=DragDropEffects.All;
			else e.Effects=DragDropEffects.None;
		}

		private void screen_Drop(object sender,DragEventArgs e)
		{
			var fileName=((string[])e.Data.GetData(DataFormats.FileDrop))[0];
			InitSoundBuffer(fileName);
		}

		private void InitSoundBuffer(string fileName)
		{
			var freqs=new[]{
				new {Center=200,Band=36},
				new {Center=500,Band=36},
				new {Center=1500,Band=36},
				new {Center=4500,Band=36},
				new {Center=9000,Band=36},
			};
			if(secondBuffer!=null) secondBuffer.Dispose();
			secondBuffer=new SecondaryBuffer(fileName,bufferDesc,DSDevice);
			effectDesc=new EffectDescription[5];
			settings=new EffectsParamEq[5];
			for(int i=0;i<effectDesc.Length;i++) effectDesc[i].GuidEffectClass=DSoundHelper.StandardParamEqGuid;
			secondBuffer.SetEffects(effectDesc);
			for(int i=0;i<effectDesc.Length;i++){
				var effectInst=(ParamEqEffect)secondBuffer.GetEffects(i);
				settings[i]=new EffectsParamEq();
				settings[i].Bandwidth=freqs[i].Band;
				settings[i].Center=freqs[i].Center;
				settings[i].Gain=0;
				effectInst.AllParameters=settings[i];
			}
			var name=new FileInfo(fileName).Name;
			var bpm=int.Parse(name.Substring(0,name.IndexOf('-')));
			timer.Interval=new TimeSpan((long)(1.0/(bpm/60.0)*1250000.0));
			//MessageBox.Show(timer.Interval.Ticks.ToString());
			return;
		}

		private void EnableGate(object sender,JointHittingEventArgs e)
		{
			prevvolume=secondBuffer.Volume;
			timer.Start();
			return;
		}

		private void DisableGate(object sender,EventArgs e)
		{
			timer.Stop();
			secondBuffer.Volume=prevvolume;
			return;
		}

		private void GateTick(object sender,EventArgs e)
		{
			if(secondBuffer.Volume!=-5000){
				secondBuffer.Volume=-5000;
			}else{
				secondBuffer.Volume=prevvolume;
			}
			return;
		}
	}
}
