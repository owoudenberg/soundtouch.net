// License :
//
// SoundTouch audio processing library
// Copyright (c) Olli Parviainen
// C# port Copyright (c) Olaf Woudenberg
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

namespace Example
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (DataContext is SoundProcessorViewModel context)
            {
#pragma warning disable CA1062 // Validate arguments of public methods
                context.OnDropFiles((string[])e.Data.GetData(DataFormats.FileDrop));
#pragma warning restore CA1062 // Validate arguments of public methods
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Reviewed: Used by xaml")]
        private void OnUpdateBinding(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.Source is TextBox source)
            {
                var binding = BindingOperations.GetBindingExpression(source, TextBox.TextProperty);
                binding?.UpdateSource();
            }
        }
    }
}
