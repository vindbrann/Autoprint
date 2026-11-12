"""Génère les fichiers de thème prêts à intégrer à partir de tokens.json.

  python3 build_theme.py

Produit :
  theme/console/autoprint-theme.css                     (Radzen 12, thème de base « standard »)
  theme/client/Autoprint.Colors.Light.xaml / .Dark.xaml  (couleurs et pinceaux, échangés selon le thème Windows)
  theme/client/Autoprint.Theme.xaml                      (polices, tailles, styles ; s'appuie sur les couleurs)
  theme/installeur/Autoprint.Installer.Theme.xaml        (autonome, clair, sans MaterialDesign, styles implicites)

Les deux dictionnaires XAML partagent les mêmes modèles de contrôles (boutons, champs, cases, listes, barres
de défilement…), sans dépendance à une bibliothèque tierce.
"""
import json, re, pathlib
from xml.dom import minidom

HERE = pathlib.Path(__file__).resolve().parent
LIV = HERE.parent
T = json.loads((LIV / 'tokens.json').read_text(encoding='utf-8'))
C = T['color']

# ---------------------------------------------------------------- utilitaires couleur
def rgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
def hexc(t): return '#%02x%02x%02x' % tuple(max(0, min(255, round(v))) for v in t)
def mix(a, b, p):  # p = part de a
    A, B = rgb(a), rgb(b); return hexc(tuple(A[i] * p + B[i] * (1 - p) for i in range(3)))
def argb(h, alpha):  # couleur WPF avec transparence : #AARRGGBB
    return '#%02X%s' % (round(alpha * 255), h.lstrip('#').upper())

def extras(theme):
    c = C[theme]; dark = theme == 'dark'
    return {
        'hover': argb(c['text'], 0.05 if dark else 0.045),
        'press': argb(c['text'], 0.09 if dark else 0.08),
        'knob-on': c['on-primary'] if dark else '#ffffff',
        'scroll-thumb': argb(c['text-muted'], 0.45),
        'pill-active': c['background'] if dark else c['surface'],
        'card-default-background': mix(c['primary-subtle'], c['surface'], 0.55),
        'card-default-border': mix(c['primary'], c['border'], 0.45),
        'success-line': mix(c['success'], c['success-subtle'], 0.25),
        'warning-line': mix(c['warning'], c['warning-subtle'], 0.25),
        'danger-line': mix(c['danger'], c['danger-subtle'], 0.25),
        'info-line': mix(c['info'], c['info-subtle'], 0.25),
        'danger-zone': mix(c['danger'], c['border'], 0.35),
        'backdrop': '#07080a' if dark else mix(c['text'], c['background'], 0.30),
    }

# ================================================================ CSS console
def css_color_block(theme):
    c = C[theme]; lines = []
    for k, v in c.items():
        if k == 'chart':
            for i, s in enumerate(v, 1): lines.append(f'  --ap-chart-{i}: {s};')
        else:
            lines.append(f'  --ap-{k}: {v};')
    e = extras(theme)
    css_hover = 'rgba(232, 234, 238, 0.05)' if theme == 'dark' else 'rgba(22, 26, 33, 0.045)'
    css_press = 'rgba(232, 234, 238, 0.09)' if theme == 'dark' else 'rgba(22, 26, 33, 0.08)'
    lines += [f'  --ap-hover: {css_hover};', f'  --ap-press: {css_press};', f"  --ap-knob-on: {e['knob-on']};"]
    if theme == 'dark':
        lines += ['  --ap-shadow-sm: none;', '  --ap-shadow-md: none;', '  --ap-shadow-lg: 0 16px 40px rgba(0, 0, 0, 0.4);']
    return '\n'.join(lines)

def css_static_block():
    f, s, r, z, m, sh = T['font'], T['space'], T['radius'], T['size'], T['motion'], T['shadow']
    L = [f"  --ap-font: {f['family']};", f"  --ap-mono: {f['family-mono']};"]
    for k, v in f['size'].items(): L.append(f'  --ap-fs-{k}: {v};')
    for k, v in s.items(): L.append(f'  --ap-s{k}: {v};')
    for k, v in r.items(): L.append(f'  --ap-r-{k}: {v};')
    for k in ('sm', 'md', 'lg'): L.append(f'  --ap-shadow-{k}: {sh[k]};')
    for k, v in z.items(): L.append(f'  --ap-{k}: {v};')
    L += [f"  --ap-fast: {m['fast']};", f"  --ap-base: {m['base']};", f"  --ap-slow: {m['slow']};", f"  --ap-ease: {m['easing']};"]
    return '\n'.join(L)

def build_css():
    src = (HERE / 'theme/autoprint-theme.src.css').read_text(encoding='utf-8')
    sem = []
    for n in ('info', 'success', 'warning', 'danger'):
        sem.append(f'  --rz-{n}: var(--ap-{n}); --rz-{n}-light: var(--ap-{n}); --rz-{n}-lighter: var(--ap-{n}-subtle); '
                   f'--rz-{n}-dark: var(--ap-{n}); --rz-{n}-darker: var(--ap-{n});')
        sem.append(f'  --rz-on-{n}: var(--ap-on-{n}); --rz-on-{n}-light: var(--ap-on-{n}); --rz-on-{n}-lighter: var(--ap-{n}); '
                   f'--rz-on-{n}-dark: var(--ap-on-{n}); --rz-on-{n}-darker: var(--ap-on-{n});')
    series = []
    for i in range(1, 25):
        series.append(f'--rz-series-{i}: var(--ap-chart-{(i - 1) % 8 + 1});')
    series = '  ' + '\n  '.join(' '.join(series[j:j + 4]) for j in range(0, 24, 4))
    rep = {
        '{{AP_LIGHT}}': css_color_block('light') + '\n' + css_static_block(),
        '{{AP_DARK}}': css_color_block('dark'),
        '{{RZ_SEMANTIC}}': '\n'.join(sem), '{{RZ_SERIES}}': series,
        '{{RADIUS_SM}}': T['radius']['sm'], '{{RADIUS_MD}}': T['radius']['md'], '{{RADIUS_LG}}': T['radius']['lg'], '{{RADIUS_XL}}': T['radius']['xl'],
        '{{CONTROL_H}}': T['size']['control-height'], '{{CONTROL_H_SM}}': T['size']['control-height-sm'], '{{CONTROL_H_LG}}': T['size']['control-height-lg'],
        '{{SIDEBAR_W}}': T['size']['sidebar-width'], '{{MOTION_FAST}}': T['motion']['fast'], '{{EASING}}': T['motion']['easing'],
    }
    for k, v in rep.items(): src = src.replace(k, v)
    assert '{{' not in src, re.findall(r'\{\{\w+\}\}', src)
    out = LIV / 'theme/console/autoprint-theme.css'; out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(src, encoding='utf-8')
    return out

# ================================================================ XAML
NS = ('xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
      '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n'
      '                    xmlns:sys="clr-namespace:System;assembly=mscorlib"')

def pascal(k): return ''.join(p.capitalize() for p in k.split('-'))

def color_entries(theme):
    c = dict(C[theme]); chart = c.pop('chart'); e = extras(theme)
    items = [(pascal(k), v) for k, v in c.items()]
    items += [(f'Chart{i}', v) for i, v in enumerate(chart, 1)]
    items += [(pascal(k), v) for k, v in e.items()]
    return items

def wpf_color(v):
    return v.upper() if v.startswith('#') and len(v) == 9 else '#FF' + v.lstrip('#').upper()

def colors_xaml(theme, header):
    rows = [f'    <!-- {header} -->']
    for name, v in color_entries(theme):
        rows.append(f'    <Color x:Key="Autoprint.Color.{name}">{wpf_color(v)}</Color>')
    rows.append('')
    for name, v in color_entries(theme):
        rows.append(f'    <SolidColorBrush x:Key="Autoprint.Brush.{name}" Color="{wpf_color(v)}" />')
    return rows

def resources_common(font_uri):
    f, r, z = T['font'], T['radius'], T['size']
    px = lambda v: v.replace('px', '')
    return f'''
    <!-- ========== Polices (IBM Plex, licence OFL) : fichiers TTF en ressource dans le dossier Fonts du projet ========== -->
    <FontFamily x:Key="Autoprint.Font">{font_uri}#IBM Plex Sans, Segoe UI</FontFamily>
    <FontFamily x:Key="Autoprint.FontMono">{font_uri}#IBM Plex Mono, Consolas</FontFamily>
    <sys:Double x:Key="Autoprint.FontSize.Xs">{px(f['size']['xs'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.Sm">{px(f['size']['sm'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.Secondary">13</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.Base">{px(f['size']['base'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.Lg">{px(f['size']['lg'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.H4">{px(f['size']['h4'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.H3">{px(f['size']['h3'])}</sys:Double>
    <sys:Double x:Key="Autoprint.FontSize.H2">22</sys:Double>

    <!-- ========== Tailles et arrondis ========== -->
    <sys:Double x:Key="Autoprint.Size.ControlHeight">{px(z['control-height'])}</sys:Double>
    <sys:Double x:Key="Autoprint.Size.ControlHeightSm">{px(z['control-height-sm'])}</sys:Double>
    <sys:Double x:Key="Autoprint.Size.ControlHeightLg">{px(z['control-height-lg'])}</sys:Double>
    <CornerRadius x:Key="Autoprint.Radius.Sm">{px(r['sm'])}</CornerRadius>
    <CornerRadius x:Key="Autoprint.Radius.Md">{px(r['md'])}</CornerRadius>
    <CornerRadius x:Key="Autoprint.Radius.Lg">{px(r['lg'])}</CornerRadius>
    <CornerRadius x:Key="Autoprint.Radius.Xl">{px(r['xl'])}</CornerRadius>
    <CornerRadius x:Key="Autoprint.Radius.Window">12</CornerRadius>
'''

CONTROLS = r'''
    <!-- ========== Focus clavier : anneau visible, uniquement à la navigation au clavier ========== -->
    <Style x:Key="Autoprint.FocusVisual">
        <Setter Property="Control.Template">
            <Setter.Value>
                <ControlTemplate>
                    <Border Margin="-3" CornerRadius="8" BorderThickness="2" BorderBrush="{DynamicResource Autoprint.Brush.FocusRing}" SnapsToDevicePixels="True" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========== Textes ========== -->
    <Style x:Key="Autoprint.Text" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="TextWrapping" Value="Wrap" />
    </Style>
    <Style x:Key="Autoprint.Text.H1" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.H2}" />
        <Setter Property="FontWeight" Value="Medium" />
    </Style>
    <Style x:Key="Autoprint.Text.H2" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.H4}" />
        <Setter Property="FontWeight" Value="Medium" />
    </Style>
    <Style x:Key="Autoprint.Text.Title" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontWeight" Value="Medium" />
    </Style>
    <Style x:Key="Autoprint.Text.Secondary" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Secondary}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
    <Style x:Key="Autoprint.Text.Caption" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Sm}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
    </Style>
    <Style x:Key="Autoprint.Text.Label" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Secondary}" />
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="Margin" Value="0,0,0,6" />
    </Style>
    <Style x:Key="Autoprint.Text.Mono" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Secondary}" />
        <Setter Property="TextWrapping" Value="NoWrap" />
    </Style>
    <Style x:Key="Autoprint.Text.Error" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.Caption}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Danger}" />
    </Style>

    <!-- ========== Boutons : 32 px, arrondi 6, casse de phrase. Variantes : Primary, Secondary, Outline, Text, Danger, Icon ========== -->
    <ControlTemplate x:Key="Autoprint.ButtonTemplate" TargetType="ButtonBase">
        <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{StaticResource Autoprint.Radius.Md}" SnapsToDevicePixels="True">
            <Grid>
                <Border x:Name="Overlay" CornerRadius="5" Background="Transparent" />
                <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
            </Grid>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Overlay" Property="Background" Value="{DynamicResource Autoprint.Brush.Hover}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="Overlay" Property="Background" Value="{DynamicResource Autoprint.Brush.Press}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
                <Setter TargetName="Bd" Property="BorderBrush" Value="Transparent" />
                <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style x:Key="Autoprint.Button.Base" TargetType="ButtonBase">
        <Setter Property="Template" Value="{StaticResource Autoprint.ButtonTemplate}" />
        <Setter Property="FocusVisualStyle" Value="{StaticResource Autoprint.FocusVisual}" />
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="Height" Value="{StaticResource Autoprint.Size.ControlHeight}" />
        <Setter Property="MinWidth" Value="32" />
        <Setter Property="Padding" Value="14,0" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
    </Style>
    <Style x:Key="Autoprint.Button.Primary" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.OnPrimary}" />
    </Style>
    <Style x:Key="Autoprint.Button.Secondary" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.BorderStrong}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
    </Style>
    <Style x:Key="Autoprint.Button.Outline" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Link}" />
    </Style>
    <Style x:Key="Autoprint.Button.Text" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Link}" />
        <Setter Property="Padding" Value="10,0" />
    </Style>
    <Style x:Key="Autoprint.Button.Danger" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Danger}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Danger}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.OnDanger}" />
    </Style>
    <Style x:Key="Autoprint.Button.Icon" TargetType="ButtonBase" BasedOn="{StaticResource Autoprint.Button.Base}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
        <Setter Property="Width" Value="{StaticResource Autoprint.Size.ControlHeight}" />
        <Setter Property="Padding" Value="0" />
    </Style>

    <!-- ========== Champs de saisie : contour à 3:1, halo au focus ========== -->
    <ControlTemplate x:Key="Autoprint.InputTemplate" TargetType="Control">
        <Grid>
            <Border x:Name="Halo" Margin="-3" CornerRadius="8" BorderThickness="3" BorderBrush="{DynamicResource Autoprint.Brush.PrimarySubtle}" Visibility="Collapsed" />
            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{StaticResource Autoprint.Radius.Md}" SnapsToDevicePixels="True">
                <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalAlignment="Center" Focusable="False"
                              HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden" />
            </Border>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
                <Setter TargetName="Halo" Property="Visibility" Value="Visible" />
            </Trigger>
            <Trigger Property="Validation.HasError" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Danger}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
                <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>
    <Style x:Key="Autoprint.InputBase" TargetType="Control">
        <Setter Property="Template" Value="{StaticResource Autoprint.InputTemplate}" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.BorderStrong}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Height" Value="{StaticResource Autoprint.Size.ControlHeight}" />
        <Setter Property="Padding" Value="8,0" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Validation.ErrorTemplate" Value="{x:Null}" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
    </Style>
    <Style x:Key="Autoprint.TextBox" TargetType="TextBox" BasedOn="{StaticResource Autoprint.InputBase}">
        <Setter Property="CaretBrush" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="SelectionBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
    </Style>
    <Style x:Key="Autoprint.TextBox.Mono" TargetType="TextBox" BasedOn="{StaticResource Autoprint.TextBox}">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Secondary}" />
    </Style>
    <Style x:Key="Autoprint.PasswordBox" TargetType="PasswordBox" BasedOn="{StaticResource Autoprint.InputBase}">
        <Setter Property="CaretBrush" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="SelectionBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
    </Style>

    <!-- ========== Liste déroulante ========== -->
    <Style x:Key="Autoprint.ComboBoxItem" TargetType="ComboBoxItem">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="Height" Value="32" />
        <Setter Property="Padding" Value="8,0" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ComboBoxItem">
                    <Border x:Name="Bd" Background="Transparent" CornerRadius="{StaticResource Autoprint.Radius.Sm}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsHighlighted" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.Hover}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Link}" />
                            <Setter Property="FontWeight" Value="Medium" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <ControlTemplate x:Key="Autoprint.ComboBoxToggleTemplate" TargetType="ToggleButton">
        <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{StaticResource Autoprint.Radius.Md}" SnapsToDevicePixels="True">
            <Path x:Name="Arrow" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,11,0" Data="M 0 0 L 4.5 4.5 L 9 0"
                  Stroke="{DynamicResource Autoprint.Brush.TextMuted}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" />
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>
    <Style x:Key="Autoprint.ComboBox" TargetType="ComboBox">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.BorderStrong}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Height" Value="{StaticResource Autoprint.Size.ControlHeight}" />
        <Setter Property="ItemContainerStyle" Value="{StaticResource Autoprint.ComboBoxItem}" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
        <Setter Property="ScrollViewer.CanContentScroll" Value="True" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ComboBox">
                    <Grid>
                        <Border x:Name="Halo" Margin="-3" CornerRadius="8" BorderThickness="3" BorderBrush="{DynamicResource Autoprint.Brush.PrimarySubtle}" Visibility="Collapsed" />
                        <ToggleButton x:Name="Toggle" Template="{StaticResource Autoprint.ComboBoxToggleTemplate}" Focusable="False" ClickMode="Press"
                                      Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                                      IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" />
                        <ContentPresenter x:Name="ContentSite" IsHitTestVisible="False" Margin="10,0,30,0" VerticalAlignment="Center" HorizontalAlignment="Left"
                                          Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                          ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" />
                        <TextBox x:Name="PART_EditableTextBox" Visibility="Hidden" Margin="8,0,30,0" VerticalAlignment="Center" Background="Transparent" BorderThickness="0"
                                 Foreground="{TemplateBinding Foreground}" FontFamily="{TemplateBinding FontFamily}" IsReadOnly="{TemplateBinding IsReadOnly}" />
                        <Popup x:Name="PART_Popup" Placement="Bottom" VerticalOffset="4" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True"
                               Focusable="False" PopupAnimation="Fade">
                            <Border x:Name="DropDown" Margin="0,0,12,12" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
                                    MaxHeight="{TemplateBinding MaxDropDownHeight}" Padding="4" CornerRadius="{StaticResource Autoprint.Radius.Lg}"
                                    Background="{DynamicResource Autoprint.Brush.Surface}" BorderBrush="{DynamicResource Autoprint.Brush.Border}" BorderThickness="1">
                                <Border.Effect>
                                    <DropShadowEffect BlurRadius="24" ShadowDepth="6" Direction="270" Opacity="0.16" Color="#10141C" />
                                </Border.Effect>
                                <ScrollViewer SnapsToDevicePixels="True">
                                    <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained" />
                                </ScrollViewer>
                            </Border>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsKeyboardFocusWithin" Value="True">
                            <Setter TargetName="Toggle" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Halo" Property="Visibility" Value="Visible" />
                        </Trigger>
                        <Trigger Property="IsEditable" Value="True">
                            <Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible" />
                            <Setter TargetName="ContentSite" Property="Visibility" Value="Hidden" />
                        </Trigger>
                        <Trigger Property="HasItems" Value="False">
                            <Setter TargetName="DropDown" Property="MinHeight" Value="40" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="Toggle" Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
                            <Setter TargetName="Toggle" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========== Case à cocher et choix unique ========== -->
    <Style x:Key="Autoprint.CheckBox" TargetType="CheckBox">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="FocusVisualStyle" Value="{StaticResource Autoprint.FocusVisual}" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="CheckBox">
                    <Grid Background="Transparent">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <Border x:Name="Box" Width="18" Height="18" CornerRadius="{StaticResource Autoprint.Radius.Sm}" BorderThickness="1.5"
                                BorderBrush="{DynamicResource Autoprint.Brush.BorderStrong}" Background="{DynamicResource Autoprint.Brush.Surface}" VerticalAlignment="Center">
                            <Path x:Name="Check" Data="M 3.5 8.5 L 7 12 L 13.5 5" Stroke="{DynamicResource Autoprint.Brush.OnPrimary}" StrokeThickness="2"
                                  StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Visibility="Collapsed" Margin="-1.5" />
                        </Border>
                        <ContentPresenter Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Box" Property="Background" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Check" Property="Visibility" Value="Visible" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="Box" Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
                            <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key="Autoprint.RadioButton" TargetType="RadioButton">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="FocusVisualStyle" Value="{StaticResource Autoprint.FocusVisual}" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RadioButton">
                    <Grid Background="Transparent">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <Ellipse x:Name="Ring" Width="18" Height="18" StrokeThickness="1.5" Stroke="{DynamicResource Autoprint.Brush.BorderStrong}"
                                 Fill="{DynamicResource Autoprint.Brush.Surface}" VerticalAlignment="Center" />
                        <ContentPresenter Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Ring" Property="StrokeThickness" Value="5" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="Ring" Property="Fill" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
                            <Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource Autoprint.Brush.Border}" />
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextDisabled}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Choix sous forme de carte (ex. installeur : SQLite / SQL Server) -->
    <Style x:Key="Autoprint.RadioCard" TargetType="RadioButton" BasedOn="{StaticResource Autoprint.RadioButton}">
        <Setter Property="Padding" Value="16,12" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RadioButton">
                    <Border x:Name="Card" CornerRadius="{StaticResource Autoprint.Radius.Lg}" BorderThickness="1" Padding="{TemplateBinding Padding}"
                            BorderBrush="{DynamicResource Autoprint.Brush.BorderStrong}" Background="{DynamicResource Autoprint.Brush.Surface}">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto" />
                                <ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <Ellipse x:Name="Ring" Width="18" Height="18" StrokeThickness="1.5" Stroke="{DynamicResource Autoprint.Brush.BorderStrong}"
                                     Fill="{DynamicResource Autoprint.Brush.Surface}" VerticalAlignment="Top" Margin="0,2,0,0" />
                            <ContentPresenter Grid.Column="1" Margin="10,0,0,0" RecognizesAccessKey="True" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Card" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Card" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Card" Property="BorderThickness" Value="2" />
                            <Setter TargetName="Card" Property="Padding" Value="15,11" />
                            <Setter TargetName="Card" Property="Background" Value="{DynamicResource Autoprint.Brush.PrimarySubtle}" />
                            <Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Ring" Property="StrokeThickness" Value="5" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========== Barre de progression (fine, arrondie) ========== -->
    <Style x:Key="Autoprint.ProgressBar" TargetType="ProgressBar">
        <Setter Property="Height" Value="6" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ProgressBar">
                    <Grid x:Name="Root" ClipToBounds="True">
                        <Border x:Name="PART_Track" CornerRadius="3" Background="{TemplateBinding Background}" />
                        <Border x:Name="PART_Indicator" CornerRadius="3" Background="{TemplateBinding Foreground}" HorizontalAlignment="Left" />
                        <Border x:Name="Moving" CornerRadius="3" Width="120" HorizontalAlignment="Left" Background="{TemplateBinding Foreground}" Visibility="Collapsed">
                            <Border.RenderTransform>
                                <TranslateTransform x:Name="MovingShift" X="-120" />
                            </Border.RenderTransform>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsIndeterminate" Value="True">
                            <Setter TargetName="PART_Indicator" Property="Visibility" Value="Collapsed" />
                            <Setter TargetName="Moving" Property="Visibility" Value="Visible" />
                            <Trigger.EnterActions>
                                <BeginStoryboard x:Name="Run">
                                    <Storyboard RepeatBehavior="Forever">
                                        <DoubleAnimation Storyboard.TargetName="MovingShift" Storyboard.TargetProperty="X" From="-120" To="800" Duration="0:0:1.6" />
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <StopStoryboard BeginStoryboardName="Run" />
                            </Trigger.ExitActions>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========== Barres de défilement fines, sans flèches ========== -->
    <Style x:Key="Autoprint.ScrollPageButton" TargetType="RepeatButton">
        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RepeatButton">
                    <Border Background="Transparent" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key="Autoprint.ScrollThumb" TargetType="Thumb">
        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Thumb">
                    <Border x:Name="Bd" Margin="2" CornerRadius="3" Background="{DynamicResource Autoprint.Brush.ScrollThumb}" />
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
                        </Trigger>
                        <Trigger Property="IsDragging" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key="Autoprint.ScrollBar" TargetType="ScrollBar">
        <Setter Property="Width" Value="10" />
        <Setter Property="MinWidth" Value="10" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ScrollBar">
                    <Grid Background="{TemplateBinding Background}">
                        <Track x:Name="PART_Track" IsDirectionReversed="True">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Style="{StaticResource Autoprint.ScrollPageButton}" Command="ScrollBar.PageUpCommand" />
                            </Track.DecreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style="{StaticResource Autoprint.ScrollThumb}" />
                            </Track.Thumb>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Style="{StaticResource Autoprint.ScrollPageButton}" Command="ScrollBar.PageDownCommand" />
                            </Track.IncreaseRepeatButton>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto" />
                <Setter Property="MinWidth" Value="0" />
                <Setter Property="Height" Value="10" />
                <Setter Property="MinHeight" Value="10" />
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="ScrollBar">
                            <Grid Background="{TemplateBinding Background}">
                                <Track x:Name="PART_Track" IsDirectionReversed="False">
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style="{StaticResource Autoprint.ScrollPageButton}" Command="ScrollBar.PageLeftCommand" />
                                    </Track.DecreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style="{StaticResource Autoprint.ScrollThumb}" />
                                    </Track.Thumb>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style="{StaticResource Autoprint.ScrollPageButton}" Command="ScrollBar.PageRightCommand" />
                                    </Track.IncreaseRepeatButton>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ========== Surfaces : cartes, encadrés, badges, étiquette d'inventaire ========== -->
    <Style x:Key="Autoprint.Card" TargetType="Border">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Lg}" />
        <Setter Property="Padding" Value="16" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
    </Style>
{CALLOUTS}
    <Style x:Key="Autoprint.Callout.Title" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>
    <Style x:Key="Autoprint.Callout.Body" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.Secondary}">
        <Setter Property="Margin" Value="0,2,0,0" />
    </Style>

    <!-- Badges de statut : toujours une icône et un texte dans le badge, pas la couleur seule -->
    <Style x:Key="Autoprint.Badge" TargetType="Border">
        <Setter Property="Height" Value="22" />
        <Setter Property="Padding" Value="6,0,8,0" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Sm}" />
        <Setter Property="HorizontalAlignment" Value="Left" />
        <Setter Property="TextElement.FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="TextElement.FontSize" Value="{StaticResource Autoprint.FontSize.Sm}" />
        <Setter Property="TextElement.FontWeight" Value="Medium" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
{BADGES}
    <Style x:Key="Autoprint.LabelTag" TargetType="Border">
        <Setter Property="Height" Value="22" />
        <Setter Property="Padding" Value="6,0" />
        <Setter Property="HorizontalAlignment" Value="Left" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Sm}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="TextElement.FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="TextElement.FontSize" Value="{StaticResource Autoprint.FontSize.Sm}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
    <Style x:Key="Autoprint.Divider" TargetType="Border">
        <Setter Property="Height" Value="1" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Divider}" />
    </Style>
'''

def callouts_and_badges():
    callouts, badges = [], []
    for n in ('Success', 'Warning', 'Danger', 'Info'):
        callouts.append(f'''    <Style x:Key="Autoprint.Callout.{n}" TargetType="Border">
        <Setter Property="Background" Value="{{DynamicResource Autoprint.Brush.{n}Subtle}}" />
        <Setter Property="BorderBrush" Value="{{DynamicResource Autoprint.Brush.{n}Line}}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="{{StaticResource Autoprint.Radius.Lg}}" />
        <Setter Property="Padding" Value="16,12" />
        <Setter Property="TextElement.Foreground" Value="{{DynamicResource Autoprint.Brush.{n}}}" />
    </Style>''')
    for key, bg, fg in (('Online', 'SuccessSubtle', 'Success'), ('Offline', 'DangerSubtle', 'Danger'), ('Warning', 'WarningSubtle', 'Warning'),
                        ('Info', 'InfoSubtle', 'Info'), ('Archived', 'SurfaceRaised', 'TextSecondary')):
        badges.append(f'''    <Style x:Key="Autoprint.Badge.{key}" TargetType="Border" BasedOn="{{StaticResource Autoprint.Badge}}">
        <Setter Property="Background" Value="{{DynamicResource Autoprint.Brush.{bg}}}" />
        <Setter Property="TextElement.Foreground" Value="{{DynamicResource Autoprint.Brush.{fg}}}" />
    </Style>''')
    badges.append('''    <Style x:Key="Autoprint.Badge.Unknown" TargetType="Border" BasedOn="{StaticResource Autoprint.Badge}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
    </Style>
    <Style x:Key="Autoprint.Badge.Neutral" TargetType="Border" BasedOn="{StaticResource Autoprint.Badge}">
        <Setter Property="Padding" Value="8,0" />
    </Style>''')
    return '\n'.join(callouts), '\n'.join(badges)

CLIENT = r'''
    <!-- ========================================================================
         Client de bureau : fenêtre, onglets en pilule, cartes d'imprimante, interrupteur, zone technique
         ======================================================================== -->
    <Style x:Key="Autoprint.Client.Window" TargetType="Border">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Window}" />
        <Setter Property="TextElement.FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
    </Style>
    <Style x:Key="Autoprint.Client.Footer" TargetType="Border">
        <Setter Property="Height" Value="48" />
        <Setter Property="Padding" Value="16,0,8,0" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Background}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Divider}" />
        <Setter Property="BorderThickness" Value="0,1,0,0" />
        <Setter Property="CornerRadius" Value="0,0,12,12" />
    </Style>
    <Style x:Key="Autoprint.Client.Place" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text}">
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.H4}" />
        <Setter Property="FontWeight" Value="Medium" />
    </Style>

    <!-- Onglets en pilule : conteneur + RadioButton (GroupName commun) -->
    <Style x:Key="Autoprint.PillTabs" TargetType="Border">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Lg}" />
        <Setter Property="Padding" Value="3" />
    </Style>
    <Style x:Key="Autoprint.PillTab" TargetType="RadioButton">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
        <Setter Property="Height" Value="30" />
        <Setter Property="Padding" Value="12,0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{StaticResource Autoprint.FocusVisual}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RadioButton">
                    <Border x:Name="Bd" CornerRadius="7" Padding="{TemplateBinding Padding}" Background="Transparent" BorderThickness="1" BorderBrush="Transparent">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Autoprint.Brush.PillActive}" />
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
                            <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key="Autoprint.PillTab.Count" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Sm}" />
        <Setter Property="FontWeight" Value="Normal" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
        <Setter Property="Margin" Value="6,0,0,0" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>

    <!-- Carte d'imprimante (et variante « par défaut ») -->
    <Style x:Key="Autoprint.PrinterCard" TargetType="Border">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Lg}" />
        <Setter Property="Padding" Value="12" />
        <Setter Property="Margin" Value="0,0,0,8" />
    </Style>
    <Style x:Key="Autoprint.PrinterCard.Default" TargetType="Border" BasedOn="{StaticResource Autoprint.PrinterCard}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.CardDefaultBackground}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.CardDefaultBorder}" />
    </Style>
    <Style x:Key="Autoprint.PrinterGlyph" TargetType="Border">
        <Setter Property="Width" Value="40" />
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Md}" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.SurfaceRaised}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
    <Style x:Key="Autoprint.InstalledDot" TargetType="Border">
        <Setter Property="Width" Value="16" />
        <Setter Property="Height" Value="16" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Success}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="BorderThickness" Value="2" />
        <Setter Property="HorizontalAlignment" Value="Right" />
        <Setter Property="VerticalAlignment" Value="Bottom" />
        <Setter Property="Margin" Value="0,0,-4,-4" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.OnSuccess}" />
    </Style>
    <!-- Badge « Mode hors ligne » du client (avertissement) : clé distincte, Autoprint.Badge.Offline existe déjà (statut) -->
    <Style x:Key="Autoprint.Client.OfflineBadge" TargetType="Border" BasedOn="{StaticResource Autoprint.Badge.Warning}" />

    <!-- Bouton étoile (favori) : ToggleButton, gris puis bleu quand coché -->
    <Style x:Key="Autoprint.FavoriteToggle" TargetType="ToggleButton" BasedOn="{StaticResource Autoprint.Button.Icon}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
        <Style.Triggers>
            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Primary}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Interrupteur -->
    <Style x:Key="Autoprint.Switch" TargetType="ToggleButton">
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{StaticResource Autoprint.FocusVisual}" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ToggleButton">
                    <Grid Background="Transparent">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <Border x:Name="Track" Width="34" Height="20" CornerRadius="10" Background="{DynamicResource Autoprint.Brush.BorderStrong}" VerticalAlignment="Center">
                            <Ellipse x:Name="Knob" Width="14" Height="14" Fill="White" HorizontalAlignment="Left" Margin="3,0,0,0">
                                <Ellipse.RenderTransform>
                                    <TranslateTransform x:Name="KnobShift" X="0" />
                                </Ellipse.RenderTransform>
                            </Ellipse>
                        </Border>
                        <ContentPresenter Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Track" Property="Background" Value="{DynamicResource Autoprint.Brush.Primary}" />
                            <Setter TargetName="Knob" Property="Fill" Value="{DynamicResource Autoprint.Brush.KnobOn}" />
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="KnobShift" Storyboard.TargetProperty="X" To="14" Duration="0:0:0.12" />
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="KnobShift" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.12" />
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.45" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Zone technique (réservée au support) : cadre en pointillés -->
    <Style x:Key="Autoprint.AdminZone" TargetType="ContentControl">
        <Setter Property="Padding" Value="16" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ContentControl">
                    <Grid>
                        <Rectangle RadiusX="10" RadiusY="10" Fill="{DynamicResource Autoprint.Brush.Background}"
                                   Stroke="{DynamicResource Autoprint.Brush.BorderStrong}" StrokeThickness="1" StrokeDashArray="4 3" SnapsToDevicePixels="True" />
                        <ContentPresenter Margin="{TemplateBinding Padding}" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
'''

INSTALLER = r'''
    <!-- ========================================================================
         Installeur : rail des étapes, pied de fenêtre, lignes de vérification
         ======================================================================== -->
    <Style x:Key="Autoprint.Installer.Window" TargetType="Window">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Surface}" />
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.Font}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Base}" />
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Text}" />
        <Setter Property="TextOptions.TextFormattingMode" Value="Display" />
    </Style>
    <Style x:Key="Autoprint.Installer.Rail" TargetType="Border">
        <Setter Property="Width" Value="216" />
        <Setter Property="Padding" Value="12,20" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Sidebar}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="0,0,1,0" />
    </Style>
    <Style x:Key="Autoprint.Installer.Footer" TargetType="Border">
        <Setter Property="Padding" Value="20,12" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Background}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="0,1,0,0" />
    </Style>
    <Style x:Key="Autoprint.Installer.Page" TargetType="Border">
        <Setter Property="Padding" Value="32,28,32,20" />
    </Style>
    <Style x:Key="Autoprint.Installer.Title" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.H1}" />
    <Style x:Key="Autoprint.Installer.Title.Success" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.H1}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Success}" />
    </Style>

    <!-- Étapes : ligne (Step), pastille numérotée (StepNumber) et leurs états -->
    <Style x:Key="Autoprint.Step" TargetType="Border">
        <Setter Property="Height" Value="34" />
        <Setter Property="Padding" Value="8,0" />
        <Setter Property="Margin" Value="0,0,0,2" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Md}" />
        <Setter Property="TextElement.FontSize" Value="{StaticResource Autoprint.FontSize.Secondary}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
    </Style>
    <Style x:Key="Autoprint.Step.Done" TargetType="Border" BasedOn="{StaticResource Autoprint.Step}">
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
    <Style x:Key="Autoprint.Step.Current" TargetType="Border" BasedOn="{StaticResource Autoprint.Step}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.PrimarySubtle}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.Link}" />
        <Setter Property="TextElement.FontWeight" Value="Medium" />
    </Style>
    <Style x:Key="Autoprint.StepNumber" TargetType="Border">
        <Setter Property="Width" Value="22" />
        <Setter Property="Height" Value="22" />
        <Setter Property="CornerRadius" Value="11" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.BorderStrong}" />
        <Setter Property="Margin" Value="0,0,10,0" />
        <Setter Property="TextElement.FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="TextElement.FontSize" Value="{StaticResource Autoprint.FontSize.Xs}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.TextSecondary}" />
    </Style>
    <Style x:Key="Autoprint.StepNumber.Done" TargetType="Border" BasedOn="{StaticResource Autoprint.StepNumber}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.SuccessSubtle}" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.Success}" />
    </Style>
    <Style x:Key="Autoprint.StepNumber.Current" TargetType="Border" BasedOn="{StaticResource Autoprint.StepNumber}">
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Primary}" />
        <Setter Property="TextElement.Foreground" Value="{DynamicResource Autoprint.Brush.OnPrimary}" />
    </Style>

    <!-- Vérification du système : conteneur et ligne de prérequis -->
    <Style x:Key="Autoprint.CheckList" TargetType="Border">
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Border}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="{StaticResource Autoprint.Radius.Lg}" />
    </Style>
    <Style x:Key="Autoprint.CheckRow" TargetType="Border">
        <Setter Property="Padding" Value="16,14" />
        <Setter Property="BorderBrush" Value="{DynamicResource Autoprint.Brush.Divider}" />
        <Setter Property="BorderThickness" Value="0,1,0,0" />
    </Style>
    <Style x:Key="Autoprint.CheckRow.First" TargetType="Border" BasedOn="{StaticResource Autoprint.CheckRow}">
        <Setter Property="BorderThickness" Value="0" />
    </Style>
    <Style x:Key="Autoprint.Text.StatusOk" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.Secondary}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Success}" />
    </Style>
    <Style x:Key="Autoprint.Text.StatusKo" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.Secondary}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.Danger}" />
    </Style>
    <Style x:Key="Autoprint.Text.StatusRunning" TargetType="TextBlock" BasedOn="{StaticResource Autoprint.Text.Secondary}">
        <Setter Property="Foreground" Value="{DynamicResource Autoprint.Brush.TextMuted}" />
    </Style>

    <!-- Journal d'installation (texte défilant en chasse fixe) -->
    <Style x:Key="Autoprint.LogBox" TargetType="TextBox" BasedOn="{StaticResource Autoprint.TextBox}">
        <Setter Property="Height" Value="Auto" />
        <Setter Property="Padding" Value="8" />
        <Setter Property="IsReadOnly" Value="True" />
        <Setter Property="TextWrapping" Value="Wrap" />
        <Setter Property="VerticalContentAlignment" Value="Top" />
        <Setter Property="Background" Value="{DynamicResource Autoprint.Brush.Background}" />
        <Setter Property="FontFamily" Value="{StaticResource Autoprint.FontMono}" />
        <Setter Property="FontSize" Value="{StaticResource Autoprint.FontSize.Sm}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border Background="{TemplateBinding Background}" BorderBrush="{DynamicResource Autoprint.Brush.Border}" BorderThickness="1"
                            CornerRadius="{StaticResource Autoprint.Radius.Md}">
                        <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalScrollBarVisibility="Auto" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========== Styles implicites : les contrôles standard de l'assistant prennent la charte sans modification ========== -->
    <Style TargetType="Button" BasedOn="{StaticResource Autoprint.Button.Secondary}" />
    <Style TargetType="TextBox" BasedOn="{StaticResource Autoprint.TextBox}" />
    <Style TargetType="PasswordBox" BasedOn="{StaticResource Autoprint.PasswordBox}" />
    <Style TargetType="ComboBox" BasedOn="{StaticResource Autoprint.ComboBox}" />
    <Style TargetType="ComboBoxItem" BasedOn="{StaticResource Autoprint.ComboBoxItem}" />
    <Style TargetType="CheckBox" BasedOn="{StaticResource Autoprint.CheckBox}" />
    <Style TargetType="RadioButton" BasedOn="{StaticResource Autoprint.RadioButton}" />
    <Style TargetType="ProgressBar" BasedOn="{StaticResource Autoprint.ProgressBar}" />
    <Style TargetType="ScrollBar" BasedOn="{StaticResource Autoprint.ScrollBar}" />
    <!-- Pas de style implicite sur TextBlock : il écraserait la couleur du texte des boutons. La police et la couleur
         par défaut passent par le style de fenêtre Autoprint.Installer.Window. -->
'''

CLIENT_IMPLICIT = r'''
    <!-- Styles implicites limités : barres de défilement fines partout. Les autres contrôles restent ceux de MaterialDesign,
         recolorés par la palette (voir LISEZMOI) ; appliquer les styles Autoprint.* au cas par cas. -->
    <Style TargetType="ScrollBar" BasedOn="{StaticResource Autoprint.ScrollBar}" />
'''

def header(title, lines):
    body = '\n'.join(f'     {l}' for l in lines)
    return f'''<?xml version="1.0" encoding="utf-8"?>
<!--
     {title}
     Généré par Docs/Charte-graphique/livrables/sources/build_theme.py à partir de tokens.json.
     Ne pas modifier à la main : modifier tokens.json ou le générateur, puis relancer le script.
{body}
-->
'''

def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')
    minidom.parseString(text.encode('utf-8'))  # XML bien formé
    return path

def check_keys(files):
    """Chaque StaticResource / DynamicResource Autoprint.* doit être défini dans l'ensemble de fichiers."""
    defined, used = set(), set()
    for f in files:
        t = f.read_text(encoding='utf-8')
        defined |= set(re.findall(r'x:Key="(Autoprint\.[^"]+)"', t))
        used |= set(re.findall(r'(?:Static|Dynamic)Resource (Autoprint\.[^}\s]+)\}', t))
    missing = sorted(used - defined)
    assert not missing, f'clés manquantes : {missing}'
    return len(defined), len(used)

def check_static_order(path):
    """Une StaticResource doit être définie plus haut dans le même fichier (sauf couleurs, fusionnées avant)."""
    t = path.read_text(encoding='utf-8'); seen = set()
    for m in re.finditer(r'x:Key="(Autoprint\.[^"]+)"|StaticResource (Autoprint\.[^}\s]+)\}', t):
        if m.group(1): seen.add(m.group(1))
        elif m.group(2) not in seen and not m.group(2).startswith(('Autoprint.Brush.', 'Autoprint.Color.')):
            raise AssertionError(f'{path.name} : {m.group(2)} utilisé en StaticResource avant sa définition')

def build_xaml():
    callouts, badges = callouts_and_badges()
    controls = CONTROLS.replace('{CALLOUTS}', callouts).replace('{BADGES}', badges)
    font_uri = 'pack://application:,,,/Fonts/'
    out = []
    # Client : couleurs séparées (clair / sombre), thème commun
    for theme, label in (('light', 'clair'), ('dark', 'sombre')):
        name = 'Light' if theme == 'light' else 'Dark'
        txt = header(f'Autoprint : couleurs du client, thème {label}',
                     ['À fusionner AVANT Autoprint.Theme.xaml. Échanger ce dictionnaire contre l\'autre quand Windows change de thème',
                      '(voir ThemeWatcher.cs et LISEZMOI.md). Clés : Autoprint.Color.* (Color) et Autoprint.Brush.* (SolidColorBrush).'])
        txt += f'<ResourceDictionary {NS}>\n' + '\n'.join(colors_xaml(theme, f'Thème {label}')) + '\n</ResourceDictionary>\n'
        out.append(write(LIV / f'theme/client/Autoprint.Colors.{name}.xaml', txt))
    txt = header('Autoprint : thème du client de bureau (WPF + MaterialDesignInXAML 5)',
                 ['Contient les polices, tailles et styles. Les couleurs viennent de Autoprint.Colors.Light.xaml ou .Dark.xaml,',
                  'fusionné juste avant ce fichier dans App.xaml. Polices : dossier Fonts du projet, action de génération « Resource ».'])
    txt += f'<ResourceDictionary {NS}>\n' + resources_common(font_uri) + controls + CLIENT + CLIENT_IMPLICIT + '</ResourceDictionary>\n'
    theme_path = write(LIV / 'theme/client/Autoprint.Theme.xaml', txt)
    out.append(theme_path)
    check_static_order(theme_path)
    print('client :', check_keys([out[0], theme_path]), 'clés définies / utilisées (clair)')
    check_keys([out[1], theme_path])
    # Installeur : autonome, clair uniquement, couleurs incluses
    txt = header('Autoprint : thème de l\'installeur du serveur (WPF, sans bibliothèque tierce, clair uniquement)',
                 ['Autonome : couleurs, polices, styles et styles implicites. À fusionner dans App.xaml de Autoprint.Installer.Server.UI.',
                  'Polices : dossier Fonts du projet, action de génération « Resource ».'])
    txt += (f'<ResourceDictionary {NS}>\n' + '\n'.join(colors_xaml('light', 'Couleurs (thème clair)')) + '\n'
            + resources_common(font_uri) + controls + INSTALLER + '</ResourceDictionary>\n')
    inst = write(LIV / 'theme/installeur/Autoprint.Installer.Theme.xaml', txt)
    check_static_order(inst)
    print('installeur :', check_keys([inst]), 'clés définies / utilisées')
    out.append(inst)
    return out

if __name__ == '__main__':
    print('css :', build_css())
    for p in build_xaml(): print('xaml :', p)
