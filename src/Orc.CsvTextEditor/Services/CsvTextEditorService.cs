﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CsvTextEditorService.cs" company="WildGums">
//   Copyright (c) 2008 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.CsvTextEditor.Services
{
    using System;
    using System.Linq;
    using System.Windows.Input;
    using Catel;
    using Catel.IoC;
    using Catel.MVVM;
    using Catel.Threading;
    using ICSharpCode.AvalonEdit;
    using ICSharpCode.AvalonEdit.Document;

    internal class CsvTextEditorService : ICsvTextEditorService
    {
        #region Fields
        private readonly ICommandManager _commandManager;

        private readonly TabSpaceElementGenerator _elementGenerator;
        private readonly TextEditor _textEditor;

        private bool _isUpdating = false;

        CommandBinding _redoBinding;
        CommandBinding _undoBinding;
        #endregion

        #region Constructors
        public CsvTextEditorService(TextEditor textEditor, ICommandManager commandManager)
        {
            Argument.IsNotNull(() => textEditor);
            Argument.IsNotNull(() => commandManager);

            _textEditor = textEditor;
            _commandManager = commandManager;

            var serviceLocator = this.GetServiceLocator();
            var typeFactory = serviceLocator.ResolveType<ITypeFactory>();
            _elementGenerator = typeFactory.CreateInstance<TabSpaceElementGenerator>();

            _textEditor.TextArea.TextView.ElementGenerators.Add(_elementGenerator);

            _textEditor.TextArea.SelectionChanged += OnTextAreaSelectionChanged;
            _textEditor.TextChanged += OnTextChanged;

            AvalonEditCommands.DeleteLine.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
        }
        #endregion

        #region Properties
        public bool IsDirty { get; set; }
        public bool HasSelection => _textEditor.SelectionLength > 0;
        public bool CanRedo => _textEditor.CanRedo;
        public bool CanUndo => _textEditor.CanUndo;
        #endregion

        #region Methods
        public void Copy()
        {
            _textEditor.Copy();
        }

        public void Cut()
        {
            _textEditor.Cut();
        }

        public void Paste()
        {
            _textEditor.Paste();
        }

        public void Redo()
        {
            _textEditor.Redo();
        }

        public void Undo()
        {
            _textEditor.Undo();
        }

        public void AddColumn()
        {
            var textDocument = _textEditor.Document;
            var linesCount = textDocument.LineCount;
            var offset = _textEditor.CaretOffset;

            var affectedLocation = textDocument.GetLocation(offset);
            var columnNumberWithOffset = _elementGenerator.GetColumn(affectedLocation);

            var columnsCount = _elementGenerator.ColumnCount;

            var columnLenght = columnNumberWithOffset.Length;
            var columnOffset = columnNumberWithOffset.OffsetInLine;

            var lineIndex = affectedLocation.Line - 1;
            var columnIndex = columnNumberWithOffset.ColumnNumber + 1;

            if (affectedLocation.Column == columnOffset)
            {
                var oldText = textDocument.Text;
                var newText = oldText.InsertCommaSeparatedColumn(columnIndex, linesCount, columnsCount);

                UpdateText(newText);
                Goto(lineIndex, columnIndex);

                return;
            }

            if (affectedLocation.Column == columnOffset - columnLenght + 1)
            {
                columnIndex--;

                var oldText = textDocument.Text;
                var newText = oldText.InsertCommaSeparatedColumn(columnIndex, linesCount, columnsCount);

                UpdateText(newText);
                Goto(lineIndex, columnIndex);
            }
        }

        public void RemoveColumn()
        {
            var textDocument = _textEditor.Document;
            var linesCount = textDocument.LineCount;
            var offset = _textEditor.CaretOffset;

            var affectedLocation = textDocument.GetLocation(offset);
            var columnNumberWithOffset = _elementGenerator.GetColumn(affectedLocation);

            var columnsCount = _elementGenerator.ColumnCount;

            var lineIndex = affectedLocation.Line - 1;
            var columnIndex = columnNumberWithOffset.ColumnNumber;

            var text = _textEditor.Text.RemoveCommaSeparatedColumn(columnIndex, linesCount, columnsCount);

            UpdateText(text);
            Goto(lineIndex, columnIndex);
        }

        public void AddLine()
        {
            var offset = _textEditor.CaretOffset;
            var textDocument = _textEditor.Document;
            var affectedLocation = textDocument.GetLocation(offset);

            var nextLineIndex = affectedLocation.Line;
            var affectedColumn = affectedLocation.Column;
            var insertOffsetInLine = affectedColumn - 1;

            if (affectedColumn == 1)
            {
                nextLineIndex--;
            }

            var columnNumberWithOffset = _elementGenerator.GetColumn(affectedLocation);

            var columnNumber = columnNumberWithOffset.ColumnNumber;
            var columnOffset = columnNumberWithOffset.OffsetInLine;

            var columnsCount = _elementGenerator.ColumnCount;
            var caretColumnIndex = columnNumber;
            if (columnNumber == columnsCount - 1 && affectedColumn == columnOffset)
            {
                caretColumnIndex = 0;
            }

            var oldText = _textEditor.Text;
            var text = oldText.InsertLineWithTextTransfer(nextLineIndex, insertOffsetInLine, columnsCount);

            UpdateText(text);
            Goto(nextLineIndex, caretColumnIndex);
        }

        public void DuplicateLine()
        {
            var textDocument = _textEditor.Document;
            var offset = _textEditor.CaretOffset;

            var affectedLocation = textDocument.GetLocation(offset);
            var columnNumberWithOffset = _elementGenerator.GetColumn(affectedLocation);

            var lineIndex = affectedLocation.Line - 1;
            var columnIndex = columnNumberWithOffset.ColumnNumber;

            var line = textDocument.Lines[lineIndex];
            var lineOffset = line.Offset;
            var endlineOffset = line.NextLine?.Offset ?? line.EndOffset;

            var text = _textEditor.Text.DuplicateTextInLine(lineOffset, endlineOffset);

            UpdateText(text);
            Goto(lineIndex + 1, columnIndex);
        }

        public void RemoveLine()
        {
            throw new NotImplementedException();
        }

        public void UpdateTextLocation(int offset, int length)
        {
            if (_isUpdating)
            {
                return;
            }

            var textDocument = _textEditor.Document;

            var affectedLocation = textDocument.GetLocation(offset);

            if (_elementGenerator.RefreshLocation(affectedLocation, length))
            {
                TaskHelper.RunAndWaitAsync(() => _textEditor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.SystemIdle, new Action(_textEditor.TextArea.TextView.Redraw)));
            }
        }

        public void UpdateText(string text)
        {
            var lines = text.Split(new[] {Environment.NewLine}, StringSplitOptions.None);

            var columnWidthByLine = lines.Select(x => x.Split(Symbols.Comma))
                .Select(x => x.Select(y => y.Length + 1).ToArray())
                .ToArray();

            _elementGenerator.Lines = columnWidthByLine;

            _isUpdating = true;
            _textEditor.Text = text;
            _isUpdating = false;
        }
        #endregion

        public void Goto(int lineIndex, int columnIndex)
        {
            _textEditor.SetCaretToSpecificLineAndColumn(lineIndex, columnIndex, _elementGenerator.Lines);
        }

        private void OnTextAreaSelectionChanged(object sender, EventArgs e)
        {
            _commandManager.InvalidateCommands();
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            _commandManager.InvalidateCommands();

            if (_undoBinding == null)
            {
                _undoBinding = new CommandBinding(
                    ApplicationCommands.Undo, new ExecutedRoutedEventHandler(UndoExecuted), null);
                _redoBinding = new CommandBinding(
                    ApplicationCommands.Redo, new ExecutedRoutedEventHandler(RedoExecuted), null);

                _textEditor.CommandBindings.Add(_undoBinding);
                _textEditor.CommandBindings.Add(_redoBinding);
            }
        }

        private void UndoExecuted(object sender, ExecutedRoutedEventArgs args)
        {
            //  ApplicationCommands.Undo.Execute(null, Application.Current.MainWindow);
        }

        private void RedoExecuted(object sender, ExecutedRoutedEventArgs args)
        {
            //    ApplicationCommands.Redo.Execute(null, Application.Current.MainWindow);
        }
    }
}