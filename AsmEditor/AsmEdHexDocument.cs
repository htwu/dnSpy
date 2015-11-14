﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

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

using dnSpy.AsmEditor.Hex;
using dnSpy.Shared.UI.HexEditor;

namespace dnSpy.AsmEditor {
	//TODO: This class should be removed once the UndoObject prop has been removed
	sealed class AsmEdHexDocument : HexDocument, IUndoHexDocument {
		public AsmEdHexDocument(string filename)
			: base(filename) {
			this.undoObject = new UndoObject(this);
		}

		public AsmEdHexDocument(byte[] data, string filename)
			: base(data, filename) {
			this.undoObject = new UndoObject(this);
		}

		public UndoObject UndoObject {
			get { return undoObject; }
		}
		readonly UndoObject undoObject;

		void IUndoHexDocument.WriteUndo(ulong startOffset, byte[] newData, string descr) {
			WriteHexUndoCommand.AddAndExecute(this, startOffset, newData, descr);
		}
	}
}