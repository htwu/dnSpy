﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using dnSpy.Contracts.Highlighting;

namespace dnSpy.Contracts.Scripting.Debugger {
	/// <summary>
	/// A stack frame in the debugged process. This stack frame is only valid until the debugged
	/// process continues.
	/// </summary>
	public interface IStackFrame {
		/// <summary>
		/// Can be called if this instance has been neutered (eg. the program has continued or the
		/// instruction pointer was changed) to get a new instance of this frame that isn't neutered.
		/// Returns null if the frame wasn't found or if the debugged process isn't paused.
		/// </summary>
		/// <returns></returns>
		IStackFrame TryGetNewFrame();

		/// <summary>
		/// true if it has been neutered. It gets neutered when the program continues or if the
		/// instruction pointer is changed. See <see cref="TryGetNewFrame"/>.
		/// </summary>
		bool IsNeutered { get; }

		/// <summary>
		/// Gets its chain
		/// </summary>
		IStackChain Chain { get; }

		/// <summary>
		/// Gets the token of the method or 0
		/// </summary>
		uint Token { get; }

		/// <summary>
		/// Start address of the stack segment
		/// </summary>
		ulong StackStart { get; }

		/// <summary>
		/// End address of the stack segment
		/// </summary>
		ulong StackEnd { get; }

		/// <summary>
		/// true if it's an IL frame
		/// </summary>
		bool IsILFrame { get; }

		/// <summary>
		/// true if it's a native frame
		/// </summary>
		bool IsNativeFrame { get; }

		/// <summary>
		/// true if it's a JIT-compiled frame (<see cref="IsILFrame"/> and <see cref="IsNativeFrame"/>
		/// are both true).
		/// </summary>
		bool IsJITCompiledFrame { get; }

		/// <summary>
		/// true if it's an internal frame
		/// </summary>
		bool IsInternalFrame { get; }

		/// <summary>
		/// true if this is a runtime unwindable frame
		/// </summary>
		bool IsRuntimeUnwindableFrame { get; }

		/// <summary>
		/// Gets the IL frame IP. Only valid if <see cref="IsILFrame"/> is true
		/// </summary>
		ILFrameIP ILFrameIP { get; }

		/// <summary>
		/// Gets the native frame IP. Only valid if <see cref="IsNativeFrame"/> is true. Writing
		/// a new value will neuter this instance.
		/// </summary>
		uint NativeOffset { get; set; }

		/// <summary>
		/// Gets the internal frame type or <see cref="InternalFrameType.None"/>
		/// if it's not an internal frame
		/// </summary>
		InternalFrameType InternalFrameType { get; }

		/// <summary>
		/// Gets the stack frame index
		/// </summary>
		int Index { get; }

		/// <summary>
		/// Gets the function or null
		/// </summary>
		IDebuggerFunction Function { get; }

		/// <summary>
		/// Gets the IL code or null
		/// </summary>
		IDebuggerCode ILCode { get; }

		/// <summary>
		/// Gets the code or null
		/// </summary>
		IDebuggerCode Code { get; }

		/// <summary>
		/// Gets all arguments
		/// </summary>
		IDebuggerValue[] ILArguments { get; }

		/// <summary>
		/// Gets all locals
		/// </summary>
		IDebuggerValue[] ILLocals { get; }

		/// <summary>
		/// Gets all generic type and/or method arguments. The first returned values are the generic
		/// type args, followed by the generic method args. See also
		/// <see cref="GenericTypeArguments"/>, <see cref="GenericMethodArguments"/> and
		/// <see cref="GetGenericArguments(out List{IDebuggerType}, out List{IDebuggerType})"/>
		/// </summary>
		IDebuggerType[] GenericArguments { get; }

		/// <summary>
		/// Gets all generic type arguments
		/// </summary>
		IDebuggerType[] GenericTypeArguments { get; }

		/// <summary>
		/// Gets all generic method arguments
		/// </summary>
		IDebuggerType[] GenericMethodArguments { get; }

		/// <summary>
		/// Gets a local variable or null if <see cref="IsILFrame"/> is false
		/// </summary>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		IDebuggerValue GetILLocal(uint index);

		/// <summary>
		/// Gets a local variable or null if <see cref="IsILFrame"/> is false
		/// </summary>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		IDebuggerValue GetILLocal(int index);

		/// <summary>
		/// Gets an argument or null if <see cref="IsILFrame"/> is false
		/// </summary>
		/// <param name="index">Index of argument</param>
		/// <returns></returns>
		IDebuggerValue GetILArgument(uint index);

		/// <summary>
		/// Gets an argument or null if <see cref="IsILFrame"/> is false
		/// </summary>
		/// <param name="index">Index of argument</param>
		/// <returns></returns>
		IDebuggerValue GetILArgument(int index);

		/// <summary>
		/// Gets all locals
		/// </summary>
		/// <param name="kind">Kind</param>
		IDebuggerValue[] GetILLocals(ILCodeKind kind);

		/// <summary>
		/// Gets a local variable or null
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		IDebuggerValue GetILLocal(ILCodeKind kind, uint index);

		/// <summary>
		/// Gets a local variable or null
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		IDebuggerValue GetILLocal(ILCodeKind kind, int index);

		/// <summary>
		/// Gets the code or null
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <returns></returns>
		IDebuggerCode GetCode(ILCodeKind kind);

		/// <summary>
		/// Splits up <see cref="GenericArguments"/> into generic type and method arguments
		/// </summary>
		/// <param name="typeGenArgs">Gets updated with a list containing all generic type arguments</param>
		/// <param name="methGenArgs">Gets updated with a list containing all generic method arguments</param>
		/// <returns></returns>
		bool GetGenericArguments(out List<IDebuggerType> typeGenArgs, out List<IDebuggerType> methGenArgs);

		/// <summary>
		/// Step into the method
		/// </summary>
		void StepInto();

		/// <summary>
		/// Step over the method
		/// </summary>
		void StepOver();

		/// <summary>
		/// Step out of the method
		/// </summary>
		void StepOut();

		/// <summary>
		/// Let the program execute until it returns to this frame
		/// </summary>
		/// <returns></returns>
		bool RunTo();

		/// <summary>
		/// Set next instruction to execute. All <see cref="IStackFrame"/> and <see cref="IDebuggerValue"/>
		/// instances will be neutered and can't be used after this method returns.
		/// </summary>
		/// <param name="offset">New offset within the current method</param>
		/// <returns></returns>
		bool SetOffset(int offset);

		/// <summary>
		/// Set next instruction to execute. All <see cref="IStackFrame"/> and <see cref="IDebuggerValue"/>
		/// instances will be neutered and can't be used after this method returns.
		/// </summary>
		/// <param name="offset">New offset within the current method</param>
		/// <returns></returns>
		bool SetOffset(uint offset);

		/// <summary>
		/// Set next instruction to execute. All <see cref="IStackFrame"/> and <see cref="IDebuggerValue"/>
		/// instances will be neutered and can't be used after this method returns.
		/// </summary>
		/// <param name="offset">New offset within the current method</param>
		/// <returns></returns>
		bool SetNativeOffset(int offset);

		/// <summary>
		/// Set next instruction to execute. All <see cref="IStackFrame"/> and <see cref="IDebuggerValue"/>
		/// instances will be neutered and can't be used after this method returns.
		/// </summary>
		/// <param name="offset">New offset within the current method</param>
		/// <returns></returns>
		bool SetNativeOffset(uint offset);

		/// <summary>
		/// Write this to <paramref name="output"/>
		/// </summary>
		/// <param name="output">Destination</param>
		/// <param name="flags">Flags</param>
		void Write(ISyntaxHighlightOutput output, TypeFormatFlags flags);

		/// <summary>
		/// ToString()
		/// </summary>
		/// <param name="flags">Flags</param>
		/// <returns></returns>
		string ToString(TypeFormatFlags flags);
	}
}