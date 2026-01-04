global using System;
global using System.IO;
global using System.Diagnostics;
global using System.IO.Compression;
global using System.Text.Json;
global using System.ComponentModel;
global using System.Runtime.CompilerServices;
global using System.Linq;
global using System.Collections.Generic;
global using System.Windows.Input;
global using System.Windows;
global using System.Windows.Controls;
global using Microsoft.Win32;
global using System.Threading.Tasks;
global using YMMResourcePackager;
global using YMMResourcePackagerPlugin.Models;
global using YukkuriMovieMaker.Commons;
global using YukkuriMovieMaker.Plugin;
global using YMMResourcePackagerPlugin.ViewModel;
global using YMMResourcePackagerPlugin.View;
namespace YMMResourcePackagerPlugin
{
    public class MyToolPlugin : IToolPlugin
    {
        public string Name => "素材同梱プラグイン";
        public Type ViewModelType => typeof(ToolViewModel);
        public Type ViewType => typeof(ToolView);
    }
}
