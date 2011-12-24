﻿/* Adapted from PIFilter.h
 * Copyright (c) 1990-1, Thomas Knoll.
 * Copyright (c) 1992-6, Adobe Systems Incorporated.
 * All rights reserved.
*/

namespace PSFilterLoad.PSApi
{
    static class FilterCase
    {

        /// filterCaseUnsupported -> -1
        public const short filterCaseUnsupported = -1;

        /// filterCaseFlatImageNoSelection -> 1
        public const short filterCaseFlatImageNoSelection = 1;

        /// filterCaseFlatImageWithSelection -> 2
        public const short filterCaseFlatImageWithSelection = 2;

        /// filterCaseFloatingSelection -> 3
        public const short filterCaseFloatingSelection = 3;

        /// filterCaseEditableTransparencyNoSelection -> 4
        public const short filterCaseEditableTransparencyNoSelection = 4;

        /// filterCaseEditableTransparencyWithSelection -> 5
        public const short filterCaseEditableTransparencyWithSelection = 5;

        /// filterCaseProtectedTransparencyNoSelection -> 6
        public const short filterCaseProtectedTransparencyNoSelection = 6;

        /// filterCaseProtectedTransparencyWithSelection -> 7
        public const short filterCaseProtectedTransparencyWithSelection = 7;
    }
}