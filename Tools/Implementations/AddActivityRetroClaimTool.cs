using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMcpServer.Utils;

namespace UnityMcpServer.Tools.Implementations
{
    public class AddActivityRetroClaimTool : ITool
    {
        public string Name => "add_activity_retro_claim";
        public string Description => "在配置文件中添加一个通用的活动奖励补领功能";

        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                activityKey = new
                {
                    type = "string",
                    description = "活动Key (例如：MyNewActivity)"
                },
                rewardType = new
                {
                    type = "string",
                    description = "奖励类型 (例如：MyNewActivityRewardType)"
                },
                multipleLanguageKey = new
                {
                    type = "string",
                    description = "多语言Key (例如：MyNewActivity)"
                }
            },
            required = new[] { "activityKey", "rewardType", "multipleLanguageKey" }
        };

        public async Task<object> ExecuteAsync(JObject args)
        {
            string activityKey = args?["activityKey"]?.ToString();
            string rewardType = args?["rewardType"]?.ToString();
            string multipleLanguageKey = args?["multipleLanguageKey"]?.ToString();

            if (string.IsNullOrEmpty(activityKey) || string.IsNullOrEmpty(rewardType) || string.IsNullOrEmpty(multipleLanguageKey))
            {
                throw new ArgumentException("Missing required arguments for AddActivityRetroClaim.");
            }

            string unityProjectPath = UnityProjectFinder.GetUnityProjectPath();
            string popupSeqConfigPath = Path.Combine(unityProjectPath, "Assets", "HotAssets", "LuaScript", "Config", "PopupSeqConfig.lua");
            string popupFunConfigPath = Path.Combine(unityProjectPath, "Assets", "HotAssets", "LuaScript", "Config", "PopupFunConfig.lua");

            // 1. 修改 PopupSeqConfig.lua
            string seqConfigContent = await File.ReadAllTextAsync(popupSeqConfigPath);
            string newSeqEntry = GetPopupSeqConfigEntry(activityKey);
            string seqAnchor = "        --淘汰赛补领";
            if (!seqConfigContent.Contains(seqAnchor))
            {
                throw new InvalidOperationException($"在 {popupSeqConfigPath} 中未找到锚点: '{seqAnchor}'");
            }
            string updatedSeqConfigContent = seqConfigContent.Replace(seqAnchor, newSeqEntry + "\n" + seqAnchor);
            await File.WriteAllTextAsync(popupSeqConfigPath, updatedSeqConfigContent);

            // 2. 修改 PopupFunConfig.lua
            string funConfigContent = await File.ReadAllTextAsync(popupFunConfigPath);
            string newFunEntries = GetPopupFunConfigEntries(activityKey, rewardType, multipleLanguageKey);
            string funAnchor = "-- 淘汰赛补领弹窗";
            if (!funConfigContent.Contains(funAnchor))
            {
                throw new InvalidOperationException($"在 {popupFunConfigPath} 中未找到锚点: '{funAnchor}'");
            }
            string updatedFunConfigContent = funConfigContent.Replace(funAnchor, newFunEntries + "\n" + funAnchor);
            await File.WriteAllTextAsync(popupFunConfigPath, updatedFunConfigContent);

            return new
            {
                content = new[]
                {
                    new { type = "text", text = $"成功为活动 '{activityKey}' 添加通用奖励补领功能。" }
                }
            };
        }

        private string GetPopupSeqConfigEntry(string activityKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine("    {");
            sb.AppendLine($"        --{activityKey}补领");
            sb.AppendLine($"        [\"key\"] = \"{activityKey}CompensationView\",");
            sb.AppendLine("        [\"daily\"] = false,");
            sb.AppendLine("        [\"downloadKey\"] = DlcNames.Base.PopTipView,");
            sb.AppendLine($"        [\"func\"] = PopupFunConfig.CheckPushNotReceiving{activityKey}RewardView,");
            sb.AppendLine($"        [\"currentNeed\"] = PopupFunConfig.need{activityKey}Reward,");
            sb.AppendLine("    },");
            return sb.ToString();
        }

        private string GetPopupFunConfigEntries(string activityKey, string rewardType, string multipleLanguageKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- {activityKey}补领弹窗");
            sb.AppendLine($"function PopupFunConfig.CheckPushNotReceiving{activityKey}RewardView(queueName)");
            sb.AppendLine($"    PopupFunConfig.CheckPushCommonNotReceivingRewardView(\"{activityKey}CompensationView\", \"{rewardType}\", \"{multipleLanguageKey}\", queueName)");
            sb.AppendLine("end");
            sb.AppendLine();
            sb.AppendLine($"function PopupFunConfig.need{activityKey}Reward()");
            sb.AppendLine($"    return PopupFunConfig.NeedNotReceivingReward(\"{rewardType}\")");
            sb.AppendLine("end");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
