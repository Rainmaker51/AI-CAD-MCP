using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using AICad.Contracts;
using AICad.LibraryScanner;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal static class Theme
{
    internal static ResourceDictionary Load(){var assembly=typeof(Theme).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith("CadToolTheme.xaml")));return (ResourceDictionary)XamlReader.Load(stream!);}
    internal static Button Button(string text,string command)=>new(){Content=text,MinHeight=38,Margin=new Thickness(0,4,0,4),HorizontalContentAlignment=HorizontalAlignment.Left,Tag=command};
}

internal sealed class GroupDialog:Window
{
    readonly ComboBox choices;internal LibraryFrameNode? Group=>choices.SelectedItem as LibraryFrameNode;
    internal GroupDialog(string title,List<LibraryFrameNode> groups,string selected)
    {
        Title=title;Width=520;Height=245;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");
        var panel=new StackPanel{Margin=new Thickness(22)};Content=panel;panel.Children.Add(new TextBlock{Text=title,FontSize=20,FontWeight=FontWeights.SemiBold});choices=new ComboBox{ItemsSource=groups.OrderBy(g=>g.Path).ToList(),DisplayMemberPath="Label",Margin=new Thickness(0,16,0,12),MinHeight=38};choices.SelectedItem=groups.FirstOrDefault(g=>g.Id==selected)??groups.FirstOrDefault();panel.Children.Add(choices);
        var ok=new Button{Content="Chọn nhóm",MinHeight=38,IsDefault=true};ok.Click+=(_,_)=>DialogResult=Group!=null;panel.Children.Add(ok);
    }
}
internal sealed class NameDialog:Window
{
    readonly TextBox value;NameDialog(string title,string label,string initial)
    {
        Title=title;Width=480;Height=230;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");var panel=new StackPanel{Margin=new Thickness(22)};Content=panel;panel.Children.Add(new TextBlock{Text=label});value=new TextBox{Text=initial,MaxLength=80,MinHeight=38,Margin=new Thickness(0,12,0,12)};panel.Children.Add(value);var ok=new Button{Content="Lưu tên nhóm",MinHeight=38,IsDefault=true};ok.Click+=(_,_)=>DialogResult=true;panel.Children.Add(ok);
    }
    internal static string? Ask(string title,string label,string initial=""){var dialog=new NameDialog(title,label,initial);return AcApp.ShowModalWindow(dialog)==true?dialog.value.Text.Trim():null;}
}
