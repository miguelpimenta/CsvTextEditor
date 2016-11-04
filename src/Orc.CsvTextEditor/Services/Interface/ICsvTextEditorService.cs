﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICsvTextEditorService.cs" company="WildGums">
//   Copyright (c) 2008 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.CsvTextEditor.Services
{
    public interface ICsvTextEditorService
    {
        #region Properties
        bool IsDirty { get; set; }
        bool HasSelection { get; }
        bool CanRedo { get; }
        bool CanUndo { get; }
        #endregion

        #region Methods
        void Copy();
        void Cut();
        void Paste();
        void Redo();
        void Undo();
        #endregion
    }
}