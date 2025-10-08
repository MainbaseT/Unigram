//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Telegram.Controls
{
    public partial class ControlEx : Control
    {
        protected void LoadTemplateChild<T>(ref T element, [CallerArgumentExpression("element")] string name = null)
            where T : DependencyObject
        {
            element ??= GetTemplateChild(name) as T;
        }

        protected void UnloadTemplateChild<T>(ref T element)
            where T : DependencyObject
        {
            if (element != null)
            {
                XamlMarkupHelper.UnloadObject(element);
                element = null;
            }
        }
    }
}
