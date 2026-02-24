//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native.Composition;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    public sealed partial class MemberTagInfoPopup : ContentPopup
    {
        public MemberTagInfoPopup(IClientService clientService, INavigationService navigationService, MessageViewModel message)
        {
            InitializeComponent();

            var tag = message.Delegate.GetMemberTag(message, out ChatMemberRank rank);
            var text = string.Empty;

            var sender = message.ClientService.GetTitle(message.SenderId);

            var inline = new InlineUIContainer();

            if (rank == ChatMemberRank.Owner)
            {
                Title = Strings.TagInfoOwnerTitle;
                text = Strings.TagInfoOwnerText;

                var color = Color.FromArgb(0xFF, 0x65, 0x60, 0xF6);

                inline.Child = new BadgeControl
                {
                    Text = tag,
                    Background = new SolidColorBrush(color) { Opacity = 0.2 },
                    Foreground = new SolidColorBrush(color.Darken()),
                    Margin = new Thickness(0, 0, 0, -4)
                };
            }
            else if (rank == ChatMemberRank.Admin)
            {
                Title = Strings.TagInfoAdminTitle;
                text = Strings.TagInfoAdminText;

                var color = Color.FromArgb(0xFF, 0x75, 0xC8, 0x73);

                inline.Child = new BadgeControl
                {
                    Text = tag,
                    Background = new SolidColorBrush(color) { Opacity = 0.2 },
                    Foreground = new SolidColorBrush(color.Darken()),
                    Margin = new Thickness(0, 0, 0, -4)
                };
            }
            else
            {
                Title = Strings.TagInfoMemberTitle;
                text = Strings.TagInfoMemberText;

                inline.Child = new TextBlock
                {
                    Text = tag,
                    Style = BootStrapper.Current.Resources["InfoBodyTextBlockStyle"] as Style
                };
            }

            // TODO: fix FormattedText.Format, replace
            var formatted = string.Format(text, sender, message.Chat.Title);
            var markdown = ClientEx.ParseMarkdown(formatted);

            var index = markdown.Text.IndexOf("un1");
            if (index != -1)
            {
                markdown.Entities.Add(new TextEntity(index, 3, new TextEntityTypeMention()));
            }

            //var prefix = text.Substring(0, index);
            //var suffix = text.Substring(index + 3);

            var paragraph = new Paragraph();
            //paragraph.Inlines.Add(prefix);
            //paragraph.Inlines.Add(inline);
            //paragraph.Inlines.Add(suffix);

            //Message.Blocks.Add(paragraph);

            var previous = 0;

            foreach (var entity in markdown.Entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    paragraph.Inlines.Add(markdown.Text.Substring(previous, entity.Offset - previous));
                }

                if (entity.Type is TextEntityTypeMention)
                {
                    paragraph.Inlines.Add(inline);
                }
                else
                {
                    paragraph.Inlines.Add(markdown.Text.Substring(entity.Offset, entity.Length), FontWeights.SemiBold);
                }

                previous = entity.Offset + entity.Length;
            }

            if (markdown.Text.Length > previous)
            {
                paragraph.Inlines.Add(markdown.Text.Substring(previous, markdown.Text.Length - previous));
            }

            Message.Blocks.Add(paragraph);

            UpdateMessage(clientService, rank);
        }

        private void UpdateMessage(IClientService clientService, ChatMemberRank rank)
        {
            BackgroundControl1.Update(clientService, null);
            Message1.UpdateMockup(clientService, Strings.TagInfoMemberTitle, ChatMemberRank.Other);
            Message1.Margin = new Thickness(0);

            if (rank == ChatMemberRank.Owner)
            {
                BackgroundControl2.Update(clientService, null);
                Message2.UpdateMockup(clientService, Strings.TagInfoOwnerTitle, ChatMemberRank.Owner);
                Message2.Margin = new Thickness(0);
            }
            else
            {
                BackgroundControl2.Update(clientService, null);
                Message2.UpdateMockup(clientService, Strings.TagInfoAdminTitle, ChatMemberRank.Admin);
                Message2.Margin = new Thickness(0);
            }
        }

        private void AlphaMask_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyAlphaMask(sender as UIElement, e.NewSize);
        }

        private void ApplyAlphaMask(UIElement element, Size newSize)
        {
            var layerVisual = CompositionDevice.GetElementLayerVisual(element);

            var compositor = layerVisual.Compositor;

            var alphaMask = new AlphaMaskEffect
            {
                Name = "AlphaMask",
                Source = new CompositionEffectSourceParameter("source"),
                AlphaMask = new CompositionEffectSourceParameter("mask")
            };

            var next = newSize.ToVector2();
            var top = 48 / next.X;

            var gradientBrush = compositor.CreateLinearGradientBrush();
            gradientBrush.StartPoint = new(0, 0);
            gradientBrush.EndPoint = new(1, 0);
            gradientBrush.MappingMode = CompositionMappingMode.Relative;
            //gradientBrush.ExtendMode = CompositionGradientExtendMode.Clamp;

            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Transparent));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(top, Colors.White));
            //gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(bottom, Colors.White));
            //gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Transparent));

            var effectFactory = compositor.CreateEffectFactory(alphaMask);
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("mask", gradientBrush);
            layerVisual.Effect = effectBrush;

        }
    }
}
