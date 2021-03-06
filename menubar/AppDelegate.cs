﻿// How to make a popup.
//
// - The window to show is in the menu
// - Set "Application is agent (UIElement)" to string value 1 in the PLIST
// - Make sure that the window we're drawing can be key see NSLOL
// - Make a sexy view that looks like a tooltip/popup - With a view NSYOLO
// - Make sure that the webview doesnt override our crap by masking and seting corner radius
// - 

using System;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using System.ComponentModel;
using MonoTouch.UIKit;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using System.Collections.Generic;
using Shortcut;

namespace menubar
{
	// We are overiding the NSView here so that we can
	// have a look and feel most like to a popup.
	[Register("NSYOLO"), DesignTimeVisible(true)]
	public class NSYOLO : NSView {
		public NSYOLO (IntPtr p) : base(p) {}
		public NSYOLO () { }

		public override void DrawRect (RectangleF dirtyRect)
		{
			int radius = 5, arrowHeight = 10, arrowWidth = 20;
			var roundedRectangleRect = new RectangleF(0, 0, this.Bounds.Width, this.Bounds.Height - arrowHeight);
			var path = new NSBezierPath ();
			path.AppendPathWithRoundedRect (roundedRectangleRect, radius, radius);

			// Draw the triangle
			path.MoveTo(new PointF((this.Bounds.Width/2)-(arrowWidth/2), this.Bounds.Height - arrowHeight));
			path.LineTo(new PointF ((this.Bounds.Width/2), this.Bounds.Height));
			path.LineTo(new PointF((this.Bounds.Width/2)+(arrowWidth/2), this.Bounds.Height - arrowHeight));
			path.ClosePath ();

			NSColor.Control.SetFill ();
			path.Fill ();
		}
	}
		
	// We need to overide the window so we can become key! Which means
	// that when are the current window in focus, and we loos key when ever someone clicks someone else. Thus 
	// enabling to hide the popup.
	[Register("NSLOL"), DesignTimeVisible(true)]
	public class NSLOL : NSWindow {
		public NSLOL (IntPtr p) : base(p) { }
		public NSLOL () {}
		public override bool CanBecomeKeyWindow { get { return true; } }
	}

	public partial class AppDelegate : NSApplicationDelegate
	{
		private NSStatusItem statusItem;
		private SizeF size = new SizeF(500,500);
		private bool pinned = false;
		private static List<string> allowed;

		public AppDelegate () { }

		public override void FinishedLaunching (NSObject notification)
		{
			allowed = new List<string>{ "alert:","notify:","showPopup:","resize:","setUserAgent:","setPinned:","setHotkey:","isVisible:","quit:","openSite:"};

			this.window.Level = NSWindowLevel.Floating;
			this.window.IsOpaque = false;
			this.window.BackgroundColor = NSColor.Clear;
			this.window.DidResignKey += (object sender, EventArgs e) => { if (!pinned) ShowPopup (false); };
	
			var	wv = this.webview2;
			wv.WantsLayer = true;
			wv.Layer.CornerRadius = 5;
			wv.Layer.MasksToBounds = true;
			wv.DrawsBackground = false;

			// Set webscript object
			wv.WindowScriptObject.SetValueForKey(this, new NSString("tray"));
			wv.MainFrame.LoadRequest(new NSUrlRequest(new NSUrl("file:///Users/emilebosch/dev/z/osx-traykit/menubar/App/Index.html")));

			statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
      statusItem.Title = "!"; 
			statusItem.HighlightMode = true;
			statusItem.Action = new Selector ("HandleMenu");

			var menu = new NSMenu ();
			menu.AddItem(new NSMenuItem("Quit", (e,v)=> {Quit();}));
			menu.AddItem(new NSMenuItem("Open Site..",(e,v)=>{ OpenSite();}));

			MASShortcut.AddGlobalHotkeyMonitor (new MASShortcut (Keycode.kVK_F12, EventModifier.NSCommandKeyMask), delegate { ShowPopup(!IsVisible()); });
			MASShortcut.AddGlobalHotkeyMonitor(new MASShortcut(Keycode.kVK_F11, EventModifier.NSCommandKeyMask), delegate { statusItem.PopUpStatusItemMenu(menu); });
		}

		[Export("HandleMenu")]
		public void click() { ShowPopup (!IsVisible()); }
			
		//**** SELECTORS

		[Export ("isSelectorExcludedFromWebScript:")]
		public static bool IsSelectorExcludedFromWebScript(MonoMac.ObjCRuntime.Selector aSelector)
		{
			return !allowed.Contains (aSelector.Name); 
		}

		[Export ("webScriptNameForSelector:")]
		public static string WebScriptNameForSelector(MonoMac.ObjCRuntime.Selector aSelector) {
			return aSelector.Name.Replace (":", "");
		}

		//**** WEB 

		[Export("notify:")]
		public void Notify(NSString title, NSString message) 
		{
			var center = NSUserNotificationCenter.DefaultUserNotificationCenter;
			center.DidDeliverNotification  += (s, e) => Console.WriteLine("Notification Delivered");
			center.DidActivateNotification += (s, e) => Console.WriteLine("Notification Touched");
			center.ShouldPresentNotification = (c, n) => { return true; };
			center.ScheduleNotification(new NSUserNotification {
				Title = title,
				InformativeText = message,
				DeliveryDate = DateTime.Now,
				SoundName = NSUserNotification.NSUserNotificationDefaultSoundName,
			});
		}

		[Export("showPopup:")]
		public void ShowPopup(bool show) {
			var windowFrame = this.window.Frame;
			windowFrame.Size = size;
			var itemFrame = ((NSWindow)statusItem.ValueForKey (new NSString ("window"))).Frame;

			// Figure out the h/w of the window. Move it to the left under the menu bar item. 
			// Then take the half of the widnows bounds and substract it to to align with the rest
			var popupFrame = new RectangleF ((itemFrame.Left - (windowFrame.Width/2)) + (itemFrame.Width/2), (itemFrame.Top - windowFrame.Height)-5, windowFrame.Width, windowFrame.Height);
			this.window.SetFrame (popupFrame, false);

			if (!show) {
				this.window.IsVisible = false;
				return;
			}

			NSApplication.SharedApplication.ActivateIgnoringOtherApps (true);
			this.window.IsVisible = show;
			this.window.MakeKeyAndOrderFront (null);
		}
	
		[Export("alert:")]
		public void ShowMessage(NSString title, NSString message){
			new MonoMac.AppKit.NSAlert {
				MessageText = title,
				InformativeText = message
			}.RunModal ();
		}

		[Export("setHotkey:")]
		public void SetHotkey(MonoMac.WebKit.WebScriptObject wo) {
			MASShortcut.AddGlobalHotkeyMonitor(new MASShortcut(Keycode.kVK_F1, EventModifier.NSCommandKeyMask), delegate { 
				wo.CallWebScriptMethod ("cb", new NSObject[]{ });
			});
		}

		[Export("quit:")]
		public void Quit() {
			NSApplication.SharedApplication.Terminate (this);
		}

		[Export("setPinned:")]
		public void SetPinned(bool value) {
			pinned = value;
		}

		[Export("openSite:")]
		public void OpenSite() {
			var opensite = new OpenSiteController ();
			opensite.Done += delegate {
				if(String.IsNullOrEmpty(opensite.Url)) return;

				ShowPopup(true);
				if(opensite.IOS == NSCellStateValue.On) SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 6_1_4 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10B350 Safari/8536.25");
				this.webview2.MainFrame.LoadRequest(new NSUrlRequest(new NSUrl(opensite.Url)));
			};

			opensite.Window.MakeKeyAndOrderFront (null);
		}

		[Export("isVisible:")]
		public bool IsVisible() {
			return this.window.IsVisible;
		}

		[Export("resize:")]
		public void Size(int height, int width){
			size = new SizeF (height, width);
			ShowPopup (this.window.IsVisible);
		}

		[Export("setUserAgent:")]
		public void SetUserAgent(string useragent) {
			this.webview2.CustomUserAgent = useragent;
		}
	}
}

