using System;
using YukkuriMovieMaker.Plugin;
using YMMResourcePackagerPlugin.ViewModel;
using YMMResourcePackagerPlugin.View;

namespace YMMResourcePackagerPlugin
{
    public class MyToolPlugin : IToolPlugin
    {
        public string Name => "素材同梱プラグイン";
        public Type ViewModelType => typeof(ToolViewModel);
        public Type ViewType => typeof(ToolView);
    }
}
