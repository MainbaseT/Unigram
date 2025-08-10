//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class SuggestedPostInfoCell : Grid
    {
        public SuggestedPostInfoCell(MessageViewModel message)
        {
            InitializeComponent();

            if (message.ReplyToItem is MessageViewModel replyTo)
            {
                var changed = new List<string>();
                var builder = new StringBuilder();

                if (!message.SuggestedPostInfo.Price.AreTheSame(replyTo.SuggestedPostInfo.Price))
                {
                    changed.Add(Strings.SuggestionOfferInfoTitleEditedPrice);
                }

                if (message.SuggestedPostInfo.SendDate != replyTo.SuggestedPostInfo.SendDate)
                {
                    changed.Add(Strings.SuggestionOfferInfoTitleEditedTime);
                }

                var oldText = replyTo.GetCaption();
                var newText = message.GetCaption();

                if (!oldText.AreTheSame(newText))
                {
                    changed.Add(Strings.SuggestionOfferInfoTitleEditedText);
                }

                var oldFile = replyTo.GetFile();
                var newFile = message.GetFile();

                if (oldFile?.Remote.UniqueId != newFile?.Remote.UniqueId)
                {
                    changed.Add(Strings.SuggestionOfferInfoTitleEditedMedia);
                }

                for (int i = 0; i < changed.Count; i++)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(i < changed.Count - 1 ? ", " : string.Format(" {0} ", Strings.SuggestionOfferInfoTitleEditedAnd));
                    }

                    builder.Append(changed[i]);
                }

                if (message.IsOutgoing)
                {
                    TextBlockHelper.SetMarkdown(Title, string.Format(Strings.SuggestionOfferInfoTitleEditedFromYou, builder.ToString()));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Title, string.Format(Strings.SuggestionOfferInfoTitleEditedFromX, message.ClientService.GetTitle(message.SenderId), builder.ToString()));
                }
            }
            else if (message.IsOutgoing)
            {
                TextBlockHelper.SetMarkdown(Title, Strings.SuggestionOfferInfoTitleYou);
            }
            else
            {
                TextBlockHelper.SetMarkdown(Title, string.Format(Strings.SuggestionOfferInfoTitle, message.ClientService.GetTitle(message.SenderId)));
            }

            if (message.SuggestedPostInfo.Price == null)
            {
                PriceTitle.Visibility = Visibility.Collapsed;
                Price.Visibility = Visibility.Collapsed;
            }
            else if (message.SuggestedPostInfo.Price is SuggestedPostPriceStar priceStar)
            {
                PriceTitle.Visibility = Visibility.Visible;
                Price.Visibility = Visibility.Visible;

                Price.Text = string.Format(Strings.StarsCountX, priceStar.StarCount);
            }
            else if (message.SuggestedPostInfo.Price is SuggestedPostPriceTon priceTon)
            {
                PriceTitle.Visibility = Visibility.Visible;
                Price.Visibility = Visibility.Visible;

                Price.Text = string.Format(Strings.TonCountX, priceTon.ToncoinCentCount / 100d);
            }

            if (message.SuggestedPostInfo.SendDate == 0)
            {
                TimeTitle.Visibility = Visibility.Collapsed;
                Time.Visibility = Visibility.Collapsed;
            }
            else
            {
                TimeTitle.Visibility = Visibility.Visible;
                Time.Visibility = Visibility.Visible;

                Time.Text = Formatter.DateAt(message.SuggestedPostInfo.SendDate);
            }
        }
    }
}
