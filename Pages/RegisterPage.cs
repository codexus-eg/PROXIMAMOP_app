using Microsoft.Maui.Controls.Shapes;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public class RegisterPage : ContentPage
{
    private readonly ActivationService _service = new();
    private readonly ChatService _chat = new();

    private readonly Entry _fullNameEntry;
    private readonly Entry _phoneEntry;
    private readonly Entry _countryEntry;
    private readonly Picker _paymentMethodPicker;
    private readonly Label _walletTitleLabel;
    private readonly Label _walletAddressLabel;
    private readonly Button _copyWalletButton;
    private readonly Border _paymentQrContainer;
    private readonly Image _paymentQrImage;
    private readonly Picker _contactMethodPicker;
    private readonly Entry _contactValueEntry;
    private readonly Entry _paymentReferenceEntry;
    private readonly Editor _userNoteEditor;
    private readonly Label _selectedImageLabel;
    private readonly Button _pickImageButton;
    private readonly Button _submitButton;

    private string _selectedImagePath = string.Empty;

    private const string UsdtTrc20WalletAddress = "TNVq4VsVSYKVkJ5PNnpwvYoy8zDumVzEDX";
    private const string UsdtErc20WalletAddress = "0x9aa0e83e585f8543ce4788e89a99bd184f2db375";
    private const string EthereumErc20WalletAddress = "0x8672d936a401019d557f7b4609d9f072a8350279";
    private const string SolanaWalletAddress = "ANpS5FgKzff1axTLfgXFPw6suknghfuAkHJEVNkckUPy";
    private const string BitcoinWalletAddress = "3EzFNDEGPXDjzs8y6avRrPkSd8t2VAuU9F";
    private const string ZainCashValue = "ضع رقم زين كاش هنا";
    private const string AsiaHawalaValue = "ضع رقم آسيا حوالة هنا";

    public RegisterPage()
    {
        Title = "طلب التفعيل";
        BackgroundColor = Colors.Black;

        var badgeLabel = new Label
        {
            Text = "PROXIMAMOP ACCESS",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#A8E6C1"),
            HorizontalOptions = LayoutOptions.Center,
            CharacterSpacing = 1.4
        };

        var titleLabel = new Label
        {
            Text = "أرسل طلب التفعيل",
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var descLabel = new Label
        {
            Text = "املأ البيانات المطلوبة، اختر طريقة الدفع، ثم ارفع صورة التحويل وانتظر مراجعة الإدارة.",
            FontSize = 14,
            TextColor = Color.FromArgb("#C8E6D3"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineHeight = 1.3
        };

        var topPanel = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#163126"), 0f),
                    new GradientStop(Color.FromArgb("#0E2019"), 1f)
                },
                new Point(0, 0),
                new Point(1, 1)),
            Stroke = Color.FromArgb("#2E5C47"),
            StrokeThickness = 1.2,
            Padding = new Thickness(18, 22),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    badgeLabel,
                    titleLabel,
                    descLabel
                }
            }
        };

        _fullNameEntry = CreateEntry("الاسم الكامل");
        _phoneEntry = CreateEntry("رقم الهاتف");
        _countryEntry = CreateEntry("الدولة");

        _paymentMethodPicker = CreatePicker("طريقة الدفع");
        _paymentMethodPicker.Items.Add("USDT TRC20");
        _paymentMethodPicker.Items.Add("USDT ERC20");
        _paymentMethodPicker.Items.Add("Ethereum ERC20");
        _paymentMethodPicker.Items.Add("SOLANA");
        _paymentMethodPicker.Items.Add("Bitcoin");
        _paymentMethodPicker.Items.Add("زين كاش");
        _paymentMethodPicker.Items.Add("آسيا حوالة");
        _paymentMethodPicker.SelectedIndexChanged += OnPaymentMethodChanged;

        _walletTitleLabel = new Label
        {
            Text = "عنوان / رابط الدفع",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            IsVisible = false
        };

        _paymentQrImage = new Image
        {
            HeightRequest = 220,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        _paymentQrContainer = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Background = Color.FromArgb("#11261D"),
            Stroke = Color.FromArgb("#2B5140"),
            StrokeThickness = 1,
            Padding = new Thickness(12),
            IsVisible = false,
            Content = _paymentQrImage
        };

        _walletAddressLabel = new Label
        {
            Text = string.Empty,
            FontSize = 13,
            TextColor = Color.FromArgb("#9FE8BE"),
            LineBreakMode = LineBreakMode.CharacterWrap,
            IsVisible = false
        };

        _copyWalletButton = new Button
        {
            Text = "نسخ الرابط / العنوان",
            BackgroundColor = Color.FromArgb("#254735"),
            TextColor = Colors.White,
            CornerRadius = 14,
            HeightRequest = 46,
            IsVisible = false
        };
        _copyWalletButton.Clicked += OnCopyWalletClicked;

        _contactMethodPicker = CreatePicker("وسيلة التواصل");
        _contactMethodPicker.Items.Add("Telegram");
        _contactMethodPicker.Items.Add("Email");
        _contactMethodPicker.Items.Add("Phone");
        _contactMethodPicker.Items.Add("WhatsApp");

        _contactValueEntry = CreateEntry("قيمة التواصل (مثال: معرف تيليجرام أو الإيميل)");
        _paymentReferenceEntry = CreateEntry("معرف التحويل / TXID (اختياري)");

        _userNoteEditor = new Editor
        {
            Placeholder = "ملاحظة إضافية (اختياري)",
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 120,
            TextColor = Colors.White,
            PlaceholderColor = Color.FromArgb("#9CB8AA"),
            BackgroundColor = Color.FromArgb("#173126")
        };

        _selectedImageLabel = new Label
        {
            Text = "لم يتم اختيار صورة التحويل",
            FontSize = 13,
            TextColor = Color.FromArgb("#D5E9DD")
        };

        _pickImageButton = new Button
        {
            Text = "اختيار صورة التحويل",
            BackgroundColor = Color.FromArgb("#254735"),
            TextColor = Colors.White,
            HeightRequest = 48,
            CornerRadius = 14
        };
        _pickImageButton.Clicked += OnPickImageClicked;

        _submitButton = new Button
        {
            Text = "إرسال الطلب",
            BackgroundColor = Color.FromArgb("#38C172"),
            TextColor = Colors.Black,
            HeightRequest = 56,
            CornerRadius = 16,
            FontAttributes = FontAttributes.Bold,
            FontSize = 17,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#50000000"),
                Radius = 18,
                Offset = new Point(0, 8),
                Opacity = 0.55f
            }
        };
        _submitButton.Clicked += OnSubmitClicked;

        var content = new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                topPanel,
                CreateSectionCard(
                    "👤 بيانات المستخدم",
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            _fullNameEntry,
                            _phoneEntry,
                            _countryEntry
                        }
                    }),

                CreateSectionCard(
                    "💳 الدفع",
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            _paymentMethodPicker,
                            _walletTitleLabel,
                            _paymentQrContainer,
                            CreateInfoBox(_walletAddressLabel),
                            _copyWalletButton,
                            _paymentReferenceEntry
                        }
                    }),

                CreateSectionCard(
                    "📞 التواصل",
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            _contactMethodPicker,
                            _contactValueEntry
                        }
                    }),

                CreateSectionCard(
                    "🖼 إثبات التحويل",
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            CreateInfoBox(_selectedImageLabel),
                            _pickImageButton
                        }
                    }),

                CreateSectionCard(
                    "📝 ملاحظات إضافية",
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            _userNoteEditor
                        }
                    }),

                _submitButton,

                new BoxView
                {
                    HeightRequest = 18,
                    Opacity = 0
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                new Image
                {
                    Source = "fire_bg.jpg",
                    Aspect = Aspect.AspectFill
                },
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = new Thickness(18, 22, 18, 12),
                        Children = { content }
                    }
                }
            }
        };
    }

    private static Entry CreateEntry(string placeholder)
    {
        return new Entry
        {
            Placeholder = placeholder,
            TextColor = Colors.White,
            PlaceholderColor = Color.FromArgb("#9CB8AA"),
            BackgroundColor = Color.FromArgb("#173126"),
            HeightRequest = 52
        };
    }

    private static Picker CreatePicker(string title)
    {
        return new Picker
        {
            Title = title,
            TitleColor = Color.FromArgb("#9CB8AA"),
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#173126"),
            HeightRequest = 52
        };
    }

    private static Border CreateInfoBox(View content)
    {
        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Background = Color.FromArgb("#11261D"),
            Stroke = Color.FromArgb("#2B5140"),
            StrokeThickness = 1,
            Padding = new Thickness(12, 10),
            IsVisible = true,
            Content = content
        };
    }
    private static Border CreateSectionCard(string title, View body)
    {
        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#10241C"), 0f),
                    new GradientStop(Color.FromArgb("#0C1B15"), 1f)
                },
                new Point(0, 0),
                new Point(1, 1)),
            Stroke = Color.FromArgb("#294A3B"),
            StrokeThickness = 1.1,
            Padding = new Thickness(16),
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#50000000"),
                Radius = 16,
                Offset = new Point(0, 6),
                Opacity = 0.45f
            },
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    },
                    body
                }
            }
        };
    }

    private void OnPaymentMethodChanged(object? sender, EventArgs e)
    {
        var selected = _paymentMethodPicker.SelectedItem?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(selected))
        {
            _walletTitleLabel.IsVisible = false;
            _walletAddressLabel.IsVisible = false;
            _copyWalletButton.IsVisible = false;
            _paymentQrContainer.IsVisible = false;
            _paymentQrImage.Source = null;
            _walletAddressLabel.Text = string.Empty;
            return;
        }

        var paymentInfo = GetPaymentInfo(selected);

        _walletTitleLabel.Text = paymentInfo.Title;
        _walletTitleLabel.IsVisible = true;

        _walletAddressLabel.Text = paymentInfo.Value;
        _walletAddressLabel.IsVisible = true;

        _copyWalletButton.IsVisible = !string.IsNullOrWhiteSpace(paymentInfo.Value);

        _paymentQrImage.Source = paymentInfo.QrImage;
        _paymentQrContainer.IsVisible = !string.IsNullOrWhiteSpace(paymentInfo.QrImage);
    }

    private PaymentInfo GetPaymentInfo(string paymentMethod)
    {
        return paymentMethod switch
        {
            "USDT TRC20" => new PaymentInfo(
                "عنوان / رابط الدفع",
                UsdtTrc20WalletAddress,
                "usdtercqr.jpg"),

            "USDT ERC20" => new PaymentInfo(
                "عنوان / رابط الدفع",
                UsdtErc20WalletAddress,
                "usdttrcqr.jpg"),

            "Ethereum ERC20" => new PaymentInfo(
                "عنوان / رابط الدفع",
                EthereumErc20WalletAddress,
                "ethereumqr.jpg"),

            "SOLANA" => new PaymentInfo(
                "عنوان / رابط الدفع",
                SolanaWalletAddress,
                "solanaqr.jpg"),

            "Bitcoin" => new PaymentInfo(
                "عنوان / رابط الدفع",
                BitcoinWalletAddress,
                "bitcoinqr.jpg"),

            "زين كاش" => new PaymentInfo(
                "رقم التحويل",
                ZainCashValue,
                "pay_zaincash.png"),

            "آسيا حوالة" => new PaymentInfo(
                "رقم التحويل",
                AsiaHawalaValue,
                "pay_asiahawala.png"),

            _ => new PaymentInfo("عنوان / رابط الدفع", string.Empty, string.Empty)
        };
    }

    private async void OnCopyWalletClicked(object? sender, EventArgs e)
    {
        var wallet = _walletAddressLabel.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(wallet))
        {
            await DisplayAlert("تنبيه", "اختر طريقة الدفع أولاً.", "حسناً");
            return;
        }
        await Clipboard.Default.SetTextAsync(wallet);
        await DisplayAlert("تم", "تم نسخ الرابط / العنوان.", "حسناً");
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();

            if (file is null)
                return;

            _selectedImagePath = file.FullPath;
            _selectedImageLabel.Text = $"✔️ تم اختيار الصورة: {file.FileName}";
            _selectedImageLabel.TextColor = Color.FromArgb("#79F2A3");
        }
        catch
        {
            await DisplayAlert("خطأ", "تعذر اختيار الصورة.", "حسناً");
        }
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (_submitButton.IsEnabled == false)
            return;

        var fullName = _fullNameEntry.Text?.Trim() ?? string.Empty;
        var phoneNumber = _phoneEntry.Text?.Trim() ?? string.Empty;
        var country = _countryEntry.Text?.Trim() ?? string.Empty;
        var paymentMethod = _paymentMethodPicker.SelectedItem?.ToString() ?? string.Empty;
        var contactMethod = _contactMethodPicker.SelectedItem?.ToString() ?? string.Empty;
        var contactValue = _contactValueEntry.Text?.Trim() ?? string.Empty;
        var paymentReference = _paymentReferenceEntry.Text?.Trim() ?? string.Empty;
        var userNote = _userNoteEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            await DisplayAlert("تنبيه", "اكتب الاسم الكامل.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            await DisplayAlert("تنبيه", "اكتب رقم الهاتف.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            await DisplayAlert("تنبيه", "اكتب الدولة.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            await DisplayAlert("تنبيه", "اختر طريقة الدفع.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(contactMethod))
        {
            await DisplayAlert("تنبيه", "اختر وسيلة التواصل.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(contactValue))
        {
            await DisplayAlert("تنبيه", "اكتب قيمة التواصل.", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedImagePath))
        {
            await DisplayAlert("تنبيه", "اختر صورة التحويل.", "حسناً");
            return;
        }

        _submitButton.IsEnabled = false;
        _submitButton.Text = "جاري الإرسال...";

        try
        {
            var deviceId = _chat.GetOrCreateDeviceId();

            var me = await _chat.GetMeAsync(deviceId);
            if (me is null)
            {
                me = await _chat.RegisterOrUpdateAsync(deviceId, fullName);
            }

            if (me is null)
            {
                await DisplayAlert("خطأ", "تعذر إنشاء أو جلب بيانات المستخدم.", "حسناً");
                return;
            }

            var request = new ActivationRequest
            {
                UserId = me.Id,
                DeviceId = deviceId,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                Country = country,
                PaymentMethod = paymentMethod,
                ContactMethod = contactMethod,
                ContactValue = contactValue,
                PaymentReference = paymentReference,
                UserNote = userNote,
                TransferImageFilePath = _selectedImagePath
            };

            var result = await _service.SubmitRequestAsync(request);

            if (!result.Success)
            {
                await DisplayAlert("خطأ", result.Message, "حسناً");
                return;
            }
            await DisplayAlert("تم", "تم إرسال طلبك بنجاح.", "حسناً");
            Application.Current!.Windows[0].Page = new NavigationPage(new PendingPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
        }
        finally
        {
            _submitButton.IsEnabled = true;
            _submitButton.Text = "إرسال الطلب";
        }
    }

    private sealed record PaymentInfo(string Title, string Value, string QrImage);
}