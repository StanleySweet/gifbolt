// <copyright file="GlobalSuppressions.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System.Diagnostics.CodeAnalysis;

// StyleCop Suppressions for accepted patterns that differ from StyleCop defaults
// but align with GifBolt coding standards

// SA1200: Using directives at file scope (GifBolt style: allows using before namespace)
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1200:Using directives should be placed correctly", Justification = "File-scoped using directives preferred")]

// SA1309: Field names should not begin with underscore
// GifBolt MANDATORY: private fields MUST use _camelCase prefix
[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with an underscore", Justification = "GifBolt standard requires _camelCase for private fields")]

// SA1412: UTF-8 BOM requirement (GifBolt uses UTF-8 without BOM)
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1412:Store files as UTF-8 with byte order mark", Justification = "GifBolt uses UTF-8 without BOM")]

// SA1600: Elements should be documented
// Suppressed for internal implementation details; public APIs require docs
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented", Justification = "Internal implementation details")]

// SA1601: Partial elements should be documented
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1601:Partial elements should be documented", Justification = "Internal implementation details")]

// SA1602: Enumeration items should be documented
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Self-documenting enum values")]
