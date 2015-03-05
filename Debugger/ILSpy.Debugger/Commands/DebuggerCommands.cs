﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Linq;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.ILSpy.Bookmarks;
using ICSharpCode.ILSpy.Debugger;
using ICSharpCode.ILSpy.Debugger.Bookmarks;
using ICSharpCode.ILSpy.Debugger.Services;
using ICSharpCode.ILSpy.Debugger.UI;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.TreeView;
using Microsoft.Win32;
using dnlib.DotNet;

namespace ICSharpCode.ILSpy.Debugger.Commands
{
	public abstract class DebuggerCommand : SimpleCommand
	{
		readonly bool? needsDebuggerActive;
		readonly bool? mustBePaused;

		protected DebuggerCommand(bool? needsDebuggerActive, bool? mustBePaused = null)
		{
			this.needsDebuggerActive = needsDebuggerActive;
			this.mustBePaused = mustBePaused;
			MainWindow.Instance.KeyUp += OnKeyUp;
			MainWindow.Instance.KeyDown += OnKeyDown;
		}

		void OnKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F5 && this is ContinueDebuggingCommand) {
				((ContinueDebuggingCommand)this).Execute(null);
				e.Handled = true;
			}
		}

		void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.SystemKey == Key.F10 ? e.SystemKey : e.Key) {
				case Key.F9:
					if (this is ToggleBreakpointCommand) {
						((ToggleBreakpointCommand)this).Execute(null);
						e.Handled = true;
					}
					break;
				case Key.F10:
					if (this is StepOverCommand) {
						((StepOverCommand)this).Execute(null);
						e.Handled = true;
					}
					break;
				case Key.F11:
					if (this is StepIntoCommand) {
						((StepIntoCommand)this).Execute(null);
						e.Handled = true;
					}
					break;
				default:
					// do nothing
					break;
			}
		}
		
		#region Static members
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern bool SetWindowPos(
			IntPtr hWnd,
			IntPtr hWndInsertAfter,
			int X,
			int Y,
			int cx,
			int cy,
			uint uFlags);

		const UInt32 SWP_NOSIZE = 0x0001;
		const UInt32 SWP_NOMOVE = 0x0002;

		static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
		static readonly IntPtr HWND_TOP = new IntPtr(0);

		static void SendWpfWindowPos(Window window, IntPtr place)
		{
			var hWnd = new WindowInteropHelper(window).Handle;
			SetWindowPos(hWnd, place, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
		}
		#endregion

		public override bool CanExecute(object parameter)
		{
			if (needsDebuggerActive == null)
				return true;
			bool b = needsDebuggerActive == (DebuggerService.CurrentDebugger != null &&
											DebuggerService.CurrentDebugger.IsDebugging);
			if (!b)
				return false;

			if (mustBePaused == null)
				return true;
			return mustBePaused == !DebuggerService.CurrentDebugger.IsProcessRunning;
		}
		
		public override void Execute(object parameter)
		{
			
		}
		
		protected static IDebugger CurrentDebugger {
			get {
				return DebuggerService.CurrentDebugger;
			}
		}
		
		protected void StartExecutable(string fileName, string workingDirectory, string arguments)
		{
			CurrentDebugger.BreakAtBeginning = DebuggerSettings.Instance.BreakAtBeginning;
			Finish();
			CurrentDebugger.Start(new ProcessStartInfo {
			                      	FileName = fileName,
			                      	WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName),
			                      	Arguments = arguments
			                      });
		}
		
		protected void StartAttaching(Process process)
		{
			CurrentDebugger.BreakAtBeginning = DebuggerSettings.Instance.BreakAtBeginning;
			Finish();
			CurrentDebugger.Attach(process);
		}
		
		protected void Finish()
		{
			EnableDebuggerUI(false);
			CurrentDebugger.DebugStopped += OnDebugStopped;
			CurrentDebugger.IsProcessRunningChanged += CurrentDebugger_IsProcessRunningChanged;
			
			MainWindow.Instance.SetStatus("Running...", Brushes.Black);
		}
		
		protected void OnDebugStopped(object sender, EventArgs e)
		{
			EnableDebuggerUI(true);
			CurrentDebugger.DebugStopped -= OnDebugStopped;
			CurrentDebugger.IsProcessRunningChanged -= CurrentDebugger_IsProcessRunningChanged;
			
			MainWindow.Instance.HideStatus();
		}
		
		protected void EnableDebuggerUI(bool enable)
		{
			// internal types
			if (enable)
				MainWindow.Instance.SessionSettings.FilterSettings.ShowInternalApi = true;
		}
		
		void CurrentDebugger_IsProcessRunningChanged(object sender, EventArgs e)
		{
			if (CurrentDebugger.IsProcessRunning) {
				//SendWpfWindowPos(this, HWND_BOTTOM);
				MainWindow.Instance.SetStatus("Running...", Brushes.Black);
				return;
			}
			
			var inst = MainWindow.Instance;
			
			// breakpoint was hit => bring to front the main window
			SendWpfWindowPos(inst, HWND_TOP); inst.Activate();
			
			// jump to type & expand folding
			if (DebugInformation.MustJumpToReference && DebugInformation.DebugStepInformation != null) {
				var method = DebugInformation.DebugStepInformation.Item3;
				if (!inst.JumpToReference(method)) {
					MessageBox.Show(MainWindow.Instance,
						string.Format("Could not find {0}\n" +
						"Make sure that it's visible in the treeview and not a hidden method or part of a hidden class. You could also try to debug the method in IL mode.", method));
				}
			}
			
			inst.SetStatus("Debugging...", Brushes.Red);
		}
	}

	class SavedDebuggedOptions
	{
		static readonly SavedDebuggedOptions Instance = new SavedDebuggedOptions();

		string Executable;
		string WorkingDirectory;
		string Arguments;

		public static ExecuteProcessWindow CreateExecWindow(string fileName, out bool? result)
		{
			var window = new ExecuteProcessWindow {
				Owner = MainWindow.Instance
			};
			var fn = fileName ?? Instance.Executable;
			if (fn != null) {
				window.SelectedExecutable = fn;
				if (fileName == null && !string.IsNullOrEmpty(Instance.WorkingDirectory))
					window.WorkingDirectory = Instance.WorkingDirectory;
			}
			window.Arguments = Instance.Arguments ?? string.Empty;

			result = window.ShowDialog();
			if (result == true) {
				Instance.Executable = window.SelectedExecutable;
				Instance.WorkingDirectory = window.WorkingDirectory;
				Instance.Arguments = window.Arguments;
			}
			return window;
		}
	}
	
	[ExportContextMenuEntryAttribute(Header = "_Debug Assembly", Icon = "Images/application-x-executable.png")]
	internal sealed class DebugExecutableNodeCommand : DebuggerCommand, IContextMenuEntry
	{
		public DebugExecutableNodeCommand()
			: base(false) {
		}

		public string GetMenuHeader(TextViewContext context)
		{
			return string.Format("_Debug {0}", ((AssemblyTreeNode)context.SelectedTreeNodes[0]).LoadedAssembly.ShortName);
		}

		public bool IsVisible(TextViewContext context)
		{
			return DebuggerService.CurrentDebugger != null && !DebuggerService.CurrentDebugger.IsDebugging &&
				context.SelectedTreeNodes != null && context.SelectedTreeNodes.All(
				delegate (SharpTreeNode n) {
					AssemblyTreeNode a = n as AssemblyTreeNode;
					if (a == null)
						return false;
					AssemblyDef asm = a.LoadedAssembly.AssemblyDefinition;
					return asm != null && asm.ManifestModule != null && (asm.ManifestModule.ManagedEntryPoint != null || asm.ManifestModule.NativeEntryPoint != 0);
				});
		}
		
		public bool IsEnabled(TextViewContext context)
		{
			return DebuggerService.CurrentDebugger != null && !DebuggerService.CurrentDebugger.IsDebugging &&
				context.SelectedTreeNodes != null && context.SelectedTreeNodes.Length == 1 &&
				context.SelectedTreeNodes[0] is AssemblyTreeNode;
		}
		
		public void Execute(TextViewContext context)
		{
			if (context.SelectedTreeNodes == null)
				return;
			if (!CurrentDebugger.IsDebugging) {
				AssemblyTreeNode n = context.SelectedTreeNodes[0] as AssemblyTreeNode;
				
				if (DebuggerSettings.Instance.AskForArguments) {
					bool? result;
					var window = SavedDebuggedOptions.CreateExecWindow(n.LoadedAssembly.FileName, out result);
					if (result == true) {
						string fileName = window.SelectedExecutable;
						
						// execute the process
						this.StartExecutable(fileName, window.WorkingDirectory, window.Arguments);
					}
				} else {
					this.StartExecutable(n.LoadedAssembly.FileName, null, null);
				}
			}
		}
	}
	
	[ExportToolbarCommand(ToolTip = "Debug an executable",
	                      ToolbarIcon = "Images/application-x-executable.png",
	                      ToolbarCategory = "Debugger",
	                      Tag = "Debugger",
	                      ToolbarOrder = 0)]
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/application-x-executable.png",
	                       MenuCategory = "Start",
	                       Header = "Debug an _executable",
	                       MenuOrder = 0)]
	internal sealed class DebugExecutableCommand : DebuggerCommand
	{
		public DebugExecutableCommand()
			: base(false) {
		}

		public override void Execute(object parameter)
		{
			if (!CurrentDebugger.IsDebugging) {
				if (DebuggerSettings.Instance.AskForArguments)
				{
					bool? result;
					var window = SavedDebuggedOptions.CreateExecWindow(null, out result);
					if (result == true) {
						string fileName = window.SelectedExecutable;
						
						// add it to references
						MainWindow.Instance.OpenFiles(new [] { fileName }, false);
						
						// execute the process
						this.StartExecutable(fileName, window.WorkingDirectory, window.Arguments);
					}
				} else {
					OpenFileDialog dialog = new OpenFileDialog() {
						Filter = ".NET Executable (*.exe) | *.exe",
						RestoreDirectory = true,
						DefaultExt = "exe"
					};
					if (dialog.ShowDialog() == true) {
						string fileName = dialog.FileName;
						
						// add it to references
						MainWindow.Instance.OpenFiles(new [] { fileName }, false);
						
						// execute the process
						this.StartExecutable(fileName, null, null);
					}
				}
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuCategory = "Start",
	                       Header = "Attach to _running application",
	                       MenuOrder = 1)]
	internal sealed class AttachCommand : DebuggerCommand
	{
		public AttachCommand()
			: base(false) {
		}

		public override void Execute(object parameter)
		{
			if (!CurrentDebugger.IsDebugging) {
				
				if (DebuggerSettings.Instance.ShowWarnings)
					MessageBox.Show("Warning: When attaching to an application, some local variables might not be available. If possible, use the \"Start Executable\" command.",
				                "Attach to a process", MessageBoxButton.OK, MessageBoxImage.Warning);
				
				var window = new AttachToProcessWindow { Owner = MainWindow.Instance };
				if (window.ShowDialog() == true) {
					StartAttaching(window.SelectedProcess);
				}
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/ContinueDebugging.png",
	                       MenuCategory = "SteppingArea",
	                       Header = "_Continue",
	                       InputGestureText = "F5",
	                       MenuOrder = 2)]
	internal sealed class ContinueDebuggingCommand : DebuggerCommand
	{
		public ContinueDebuggingCommand()
			: base(true, true) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging && !CurrentDebugger.IsProcessRunning) {
				CurrentLineBookmark.Remove();
				CurrentDebugger.Continue();
				MainWindow.Instance.SetStatus("Running...", Brushes.Black);
			}
		}
	}

	[ExportMainMenuCommand(Menu = "_Debug",
						   MenuIcon = "Images/Break.png",
						   MenuCategory = "SteppingArea",
						   Header = "Brea_k",
						   MenuOrder = 2.1)]
	internal sealed class BreakDebuggingCommand : DebuggerCommand
	{
		public BreakDebuggingCommand()
			: base(true, false) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging && CurrentDebugger.IsProcessRunning)
			{
				CurrentDebugger.Break();
				MainWindow.Instance.SetStatus("Debugging...", Brushes.Red);
			}
		}
	}

	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/StepInto.png",
	                       MenuCategory = "SteppingArea",
	                       Header = "Step _Into",
	                       InputGestureText = "F11",
	                       MenuOrder = 3)]
	internal sealed class StepIntoCommand : DebuggerCommand
	{
		public StepIntoCommand()
			: base(true, true) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging && !CurrentDebugger.IsProcessRunning) {
				base.Execute(null);
				CurrentDebugger.StepInto();
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/StepOver.png",
	                       MenuCategory = "SteppingArea",
	                       Header = "Step _Over",
	                       InputGestureText = "F10",
	                       MenuOrder = 4)]
	internal sealed class StepOverCommand : DebuggerCommand
	{
		public StepOverCommand()
			: base(true, true) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging && !CurrentDebugger.IsProcessRunning) {
				base.Execute(null);
				CurrentDebugger.StepOver();
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/StepOut.png",
	                       MenuCategory = "SteppingArea",
	                       Header = "Step Ou_t",
	                       MenuOrder = 5)]
	internal sealed class StepOutCommand : DebuggerCommand
	{
		public StepOutCommand()
			: base(true, true) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging && !CurrentDebugger.IsProcessRunning) {
				base.Execute(null);
				CurrentDebugger.StepOut();
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuCategory = "SteppingArea",
	                       Header = "_Detach from running application",
	                       MenuOrder = 6)]
	internal sealed class DetachCommand : DebuggerCommand
	{
		public DetachCommand()
			: base(true) {
		}

		public override void Execute(object parameter)
		{
			if (CurrentDebugger.IsDebugging){
				CurrentDebugger.Detach();
				
				EnableDebuggerUI(true);
				CurrentDebugger.DebugStopped -= OnDebugStopped;
			}
		}
	}
	
	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuIcon = "Images/DeleteAllBreakpoints.png",
	                       MenuCategory = "Others",
	                       Header = "Remove all _breakpoints",
	                       MenuOrder = 7.9)]
	internal sealed class RemoveBreakpointsCommand : DebuggerCommand
	{
		public RemoveBreakpointsCommand()
			: base(null) {
		}

		public override void Execute(object parameter)
		{
			for (int i = BookmarkManager.Bookmarks.Count - 1; i >= 0; --i) {
				var bookmark = BookmarkManager.Bookmarks[i];
				if (bookmark is BreakpointBookmark) {
					BookmarkManager.RemoveMark(bookmark);
				}
			}
		}
	}

	[ExportMainMenuCommand(Menu = "_Debug",
	                       MenuCategory = "Others",
	                       Header = "To_ggle Breakpoint",
						   InputGestureText = "F9",
	                       MenuOrder = 7)]
	internal sealed class ToggleBreakpointCommand : DebuggerCommand
	{
		public ToggleBreakpointCommand()
			: base(null) {
		}

		public override void Execute(object parameter)
		{
			var location = MainWindow.Instance.TextView.TextEditor.TextArea.Caret.Location;
			BreakpointHelper.Toggle(location.Line, location.Column);
		}
	}
}
