using System.Management;
using System.Text.RegularExpressions;

namespace CpuPerfGrader
{
    public static class CpuPerfHelper
    {
        // 性能分级阈值
        private static readonly int _lowPerfCoreCount = 4;     // 小于等于4核心4线程是低性能
        private static readonly int _lowPerfThreadCount = 4;   // 小于等于4核心4线程是低性能

        private static readonly double _highPerfFreq = 2600;    // 大于2.6Ghz频率并满足其他条件是高性能
        private static readonly double _lowPerfFreq = 2000;     // 小于2.0Ghz频率是低性能

        private static readonly int _highPerfYear = 2016;
        private static readonly int _midPerfYear = 2011;

        // 暂定同代CPU笔记本比台式机落后一年性能
        private static readonly int _laptopPerfYearOffset = 1;

        /// <summary>
        /// 获取当前CPU的性能等级
        /// </summary>
        /// <returns></returns>
        public static CPUPerfLevel GetCpuPerfGrade(string cpuName, uint coreCount, uint threadCount, uint frequency)
        {
            try
            {
                // AMD CPU频率比较虚 减去300Mhz
                if (cpuName.ToLower().Contains("amd") && frequency > 0)
                {
                    frequency -= 300;
                }

                if ((frequency > 0 && frequency <= _lowPerfFreq) ||
                    (coreCount > 0 && coreCount <= _lowPerfCoreCount && threadCount > 0 && threadCount <= _lowPerfThreadCount))
                {
                    // 4核4线程及以下的认为是低性能
                    // 2.4Ghz及以下的认为是低性能
                    return CPUPerfLevel.Low;
                }

                if (frequency >= _highPerfFreq && (coreCount > _lowPerfCoreCount && threadCount > _lowPerfThreadCount))
                {
                    // 频率大于等于2.6Ghz,认为是高性能
                    return CPUPerfLevel.High;
                }

                if (frequency > _lowPerfFreq && (coreCount > _lowPerfCoreCount && threadCount > _lowPerfThreadCount))
                {
                    // 频率大于2.0Ghz小于2.6Ghz,认为是中性能
                    return CPUPerfLevel.Mid;
                }

                bool cpuInfoGotten = GetCpuModel(cpuName, out CpuFamily family, out CpuPlatform platform, out int introductionYear);

                if (cpuInfoGotten && introductionYear > 0)
                {
                    // 能够识别CPU型号
                    // 根据平台和频率评定发布年份是否需要偏移(暂定同代CPU笔记本比台式机落后一年性能)
                    int offset = platform == CpuPlatform.Desktop ? 0 : (platform == CpuPlatform.Mobile ? _laptopPerfYearOffset : (frequency < _lowPerfFreq ? _laptopPerfYearOffset : 0));
                    introductionYear -= offset;
                    return introductionYear >= _highPerfYear ? CPUPerfLevel.High : (introductionYear >= _midPerfYear ? CPUPerfLevel.Mid : CPUPerfLevel.Low);
                }

                return CPUPerfLevel.Mid;
            }
            catch (Exception) { }
            return CPUPerfLevel.Mid;
        }

        /// <summary>
        /// 获取CPU名称
        /// </summary>
        /// <returns></returns>
        public static string GetCpuName()
        {
            try
            {
                ManagementClass managementClass = new("win32_processor");
                if (managementClass != null)
                {
                    ManagementObjectCollection objectCollection = managementClass.GetInstances();
                    if (objectCollection != null)
                    {
                        foreach (ManagementObject obj in objectCollection.Cast<ManagementObject>())
                        {
                            string? cpuName = obj["Name"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(cpuName))
                            {
                                return cpuName;
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return string.Empty;
        }

        /// <summary>
        /// 获取CPU信息，核心数、线程数、最大频率
        /// </summary>
        /// <param name="cpuNumberOfEnabledCore">核心数</param>
        /// <param name="cpuThreadCount">线程数</param>
        /// <param name="cpuMaxClockSpeed">最大频率</param>
        /// <returns></returns>
        public static bool GetCpuInfo(out uint cpuNumberOfEnabledCore, out uint cpuThreadCount, out uint cpuMaxClockSpeed)
        {
            cpuNumberOfEnabledCore = 0;
            cpuThreadCount = 0;
            cpuMaxClockSpeed = 0;

            try
            {
                ManagementClass managementClass = new("win32_processor");
                if (managementClass != null)
                {
                    ManagementObjectCollection objectCollection = managementClass.GetInstances();
                    if (objectCollection != null)
                    {
                        foreach (ManagementObject obj in objectCollection.Cast<ManagementObject>())
                        {
                            var numberOfEnabledCore = obj["NumberOfEnabledCore"];
                            if (numberOfEnabledCore is uint core && core > 0)
                            {
                                cpuNumberOfEnabledCore = core;
                                break;
                            }
                        }

                        foreach (ManagementObject obj in objectCollection.Cast<ManagementObject>())
                        {
                            var threadCount = obj["ThreadCount"];
                            if (threadCount is uint count && count > 0)
                            {
                                cpuThreadCount = count;
                                break;
                            }
                        }

                        foreach (ManagementObject obj in objectCollection.Cast<ManagementObject>())
                        {
                            var maxClockSpeed = obj["MaxClockSpeed"];
                            if (maxClockSpeed is uint speed && speed > 0)
                            {
                                cpuMaxClockSpeed = speed;
                                break;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>
        /// 获取CPU型号
        /// </summary>
        /// <param name="cpuName"></param>
        /// <param name="family"></param>
        /// <param name="platform"></param>
        /// <param name="introductionYear"></param>
        /// <returns></returns>
        private static bool GetCpuModel(string cpuName, out CpuFamily family, out CpuPlatform platform, out int introductionYear)
        {
            try
            {
                cpuName = cpuName?.ToLower() ?? "";
                if (cpuName.Contains("intel") || cpuName.Contains("英特尔"))
                {
                    // Intel
                    cpuName = cpuName.Replace("cpu ", "")
                                     .Replace("(r)", "")
                                     .Replace("(tm)", "")
                                     .Replace("processor ", "");

                    // 根据系列区分处理，不同系列命名规则不同
                    if (cpuName.Contains("core"))
                    {
                        cpuName = cpuName.Replace("coret", "core");// 某些情况下检测到的CPU型号会带t
                        Match match = Regex.Match(cpuName, @"(core [mi][\d])[ |-]([\w]+)");
                        string coreFamily = match.Groups[1].Value;
                        string coreModel = match.Groups[2].Value;

                        Match modelMatch;
                        if (Regex.IsMatch(coreModel, @"[\w]+g[\d]"))
                        {
                            // 十代之后会有后缀g1、g2、g3等的型号，目前SKU值可能为两位数字或者两位数字加一个字母n
                            modelMatch = Regex.Match(coreModel, @"([\d]+)[0-9][0-9]n*([a-z]*[\d]*)");
                        }
                        else
                        {
                            modelMatch = Regex.Match(coreModel, @"([\d]+)[0-9a-z][0-9a-z][0-9]([a-z]*[\d]*)");
                        }

                        string coreGeneration = modelMatch.Groups[1].Value;
                        string corePlatform = modelMatch.Groups[2].Value;

                        family = CpuFamily.Intel_Core;

                        platform = corePlatform switch
                        {
                            "h" => CpuPlatform.Mobile,
                            "hk" => CpuPlatform.Mobile,
                            "hx" => CpuPlatform.Mobile,
                            "p" => CpuPlatform.Mobile,
                            "u" => CpuPlatform.Mobile,
                            "y" => CpuPlatform.Mobile,
                            "m" => CpuPlatform.Mobile,
                            "q" => CpuPlatform.Mobile,
                            "l" => CpuPlatform.Mobile,
                            "qm" => CpuPlatform.Mobile,
                            "mq" => CpuPlatform.Mobile,
                            "g1" => CpuPlatform.Mobile,
                            "g2" => CpuPlatform.Mobile,
                            "g3" => CpuPlatform.Mobile,
                            "g4" => CpuPlatform.Mobile,
                            "g5" => CpuPlatform.Mobile,
                            "g6" => CpuPlatform.Mobile,
                            "g7" => CpuPlatform.Mobile,
                            _ => CpuPlatform.Desktop,
                        };

                        introductionYear = coreGeneration switch
                        {
                            "1" => 2010,
                            "2" => 2011,
                            "3" => 2012,
                            "4" => 2013,
                            "5" => 2014,
                            "6" => 2015,
                            "7" => 2016,
                            "8" => 2017,
                            "9" => 2018,
                            "10" => 2020,
                            "11" => 2021,
                            "12" => 2022,
                            "13" => 2023,
                            "14" => 2024,
                            "15" => 2025,
                            "16" => 2026,
                            _ => -1,
                        };

                        // Core 2 已经停产 2006
                        if (cpuName.Contains("core2"))
                        {
                            family = CpuFamily.Unkonwn;
                            introductionYear = 2006;
                        }

                        if (introductionYear == -1)
                        {
                            // 无法识别的代数，认为是新款
                            introductionYear = 2024;
                        }

                        return true;
                    }
                    else if (cpuName.Contains("xeon"))
                    {
                        family = CpuFamily.Intel_Xeon;
                        platform = CpuPlatform.Desktop;
                        introductionYear = -1;

                        if (cpuName.Contains("bronze 31"))
                        {
                            introductionYear = 2017;
                        }
                        else if (cpuName.Contains("bronze 32"))
                        {
                            introductionYear = 2020;
                        }
                        else if (cpuName.Contains("silver 41") || cpuName.Contains("gold 61") || cpuName.Contains("gold 51") || cpuName.Contains("platinum 81"))
                        {
                            introductionYear = 2017;
                        }
                        else if (cpuName.Contains("silver 42") || cpuName.Contains("gold 62") || cpuName.Contains("gold 52") || cpuName.Contains("platinum 82") || cpuName.Contains("platinum 92"))
                        {
                            introductionYear = 2019;
                        }
                        else if (cpuName.Contains("silver 43") || cpuName.Contains("gold 63") || cpuName.Contains("gold 53") || cpuName.Contains("platinum 83"))
                        {
                            introductionYear = 2021;
                        }
                        else if (cpuName.Contains("xeon e-"))
                        {
                            // Xeon E 系列
                            if (cpuName.Contains("e-21"))
                            {
                                introductionYear = 2018;
                            }
                            else if (cpuName.Contains("e-22"))
                            {
                                introductionYear = 2019;
                            }
                            else if (cpuName.Contains("e-23"))
                            {
                                introductionYear = 2021;
                            }
                        }
                        else if (cpuName.Contains("xeon d-"))
                        {
                            // Xeon D 系列
                            if (cpuName.Contains("d-15"))
                            {
                                introductionYear = 2017;
                            }
                            else if (cpuName.Contains("d-21"))
                            {
                                introductionYear = 2018;
                            }
                            else if (cpuName.Contains("d-16"))
                            {
                                introductionYear = 2019;
                            }
                            else if (cpuName.Contains("d-27"))
                            {
                                introductionYear = 2022;
                            }
                            else if (cpuName.Contains("d-17"))
                            {
                                introductionYear = 2022;
                            }
                        }
                        else if (cpuName.Contains("xeon w-"))
                        {
                            // Xeon W 系列
                            if (cpuName.Contains("w-21"))
                            {
                                introductionYear = 2017;
                            }
                            else if (cpuName.Contains("w-31"))
                            {
                                introductionYear = 2018;
                            }
                            else if (cpuName.Contains("w-22"))
                            {
                                introductionYear = 2019;
                            }
                            else if (cpuName.Contains("w-32"))
                            {
                                introductionYear = 2019;
                            }
                            else if (cpuName.Contains("w-33"))
                            {
                                introductionYear = 2021;
                            }
                            else if (cpuName.Contains("w-10") || cpuName.Contains("w-12"))
                            {
                                introductionYear = 2020;
                            }
                            else if (cpuName.Contains("w-11") || cpuName.Contains("w-13"))
                            {
                                introductionYear = 2021;
                            }
                        }
                        else if (cpuName.Contains("xeon x"))
                        {
                            // Xeon X 系列
                            introductionYear = 2010;
                        }
                        else if (cpuName.Contains("xeon e7") && cpuName.Contains("v2"))
                        {
                            // Xeon E7 系列
                            introductionYear = 2014;
                        }
                        else if (cpuName.Contains("xeon e5"))
                        {
                            // Xeon E5 系列
                            if (cpuName.Contains("v2"))
                            {
                                introductionYear = 2013;
                            }
                            else if (cpuName.Contains("v3"))
                            {
                                introductionYear = 2014;
                            }
                            else if (cpuName.Contains("v4"))
                            {
                                introductionYear = 2016;
                            }
                        }
                        else if (cpuName.Contains("xeon e3"))
                        {
                            // Xeon E3 系列
                            if (cpuName.Contains("v3"))
                            {
                                introductionYear = 2013;
                            }
                            else if (cpuName.Contains("v5"))
                            {
                                introductionYear = 2016;
                            }
                            else if (cpuName.Contains("v6"))
                            {
                                introductionYear = 2017;
                            }
                        }
                        else if (cpuName.Contains("xeon w3") || cpuName.Contains("xeon w5") || cpuName.Contains("xeon w7") || cpuName.Contains("xeon w9"))
                        {
                            // Xeon W 系列
                            if (cpuName.Contains("xeon w3-24") ||
                                cpuName.Contains("xeon w5-24") ||
                                cpuName.Contains("xeon w5-34") ||
                                cpuName.Contains("xeon w7-24") ||
                                cpuName.Contains("xeon w7-34") ||
                                cpuName.Contains("xeon w9-34"))
                            {
                                introductionYear = 2023;
                            }
                        }

                        if (introductionYear == -1)
                        {
                            // 无法识别的代数，认为是新款
                            introductionYear = 2024;
                        }

                        return true;
                    }
                    else if (cpuName.Contains("pentium"))
                    {
                        // 2022最后一款
                        family = CpuFamily.Intel_Pentium;
                        platform = CpuPlatform.Desktop;
                        introductionYear = 2016;
                        return true;
                    }
                    else if (cpuName.Contains("celeron"))
                    {
                        // 2022最后一款
                        family = CpuFamily.Intel_Celeron;
                        platform = CpuPlatform.Desktop;
                        Match match = Regex.Match(cpuName, @"celeron [a-z]*([\d])([\d]+)[\w]* ");
                        string celeronGeneration = match.Groups[1].Value;
                        string celeronSKU = match.Groups[2].Value;

                        introductionYear = celeronGeneration switch
                        {
                            "6" => 2022,
                            "5" => 2020,
                            "4" => 2018,
                            "3" => 2016,
                            "2" => 2013,
                            "1" => 2013,
                            _ => 2022,
                        };

                        // SKU 太短，应当是很老的型号，后续的赛扬SKU长度多为3位，远古时期的会有2位
                        if (celeronSKU.Length <= 2)
                        {
                            introductionYear = 2012;
                        }

                        return true;
                    }
                    else if (cpuName.Contains("itanium"))
                    {
                        // 2017年最后一款
                        family = CpuFamily.Intel_Itanium;
                        platform = CpuPlatform.Desktop;
                        introductionYear = 2017;
                        return true;
                    }
                    else if (cpuName.Contains("atom"))
                    {
                        // Atom面向低端市场
                        family = CpuFamily.Intel_Atom;
                        platform = CpuPlatform.Mobile;
                        introductionYear = 2010;
                        return true;
                    }
                }
                else if (cpuName.Contains("amd"))
                {
                    // AMD
                    cpuName = cpuName.Replace("with radeon graphics", "")
                                     .Replace("processor ", "")
                                     .Replace("with radeon vega graphics", "");

                    // 根据系列区分处理，不同系列命名规则不同
                    if (cpuName.Contains("ryzen"))
                    {
                        Match match = Regex.Match(cpuName, @"ryzen[\w()]* ([\d])[ pro]* ([\d]+)[\d][\d][\d]([a-z]*[\w]*) ");
                        string ryzenFamily = match.Groups[1].Value;
                        string ryzenGeneration = match.Groups[2].Value;
                        string ryzenPlatform = match.Groups[3].Value;

                        family = CpuFamily.AMD_Ryzen;

                        platform = ryzenPlatform switch
                        {
                            "h" => CpuPlatform.Mobile,
                            "hs" => CpuPlatform.Mobile,
                            "hx" => CpuPlatform.Mobile,
                            "hx3d" => CpuPlatform.Mobile,
                            "u" => CpuPlatform.Mobile,
                            "c" => CpuPlatform.Mobile,
                            "e" => CpuPlatform.Mobile,// 2代桌面端也有E后缀，但是后续移动端会有E后缀表示无风扇
                            _ => CpuPlatform.Desktop,
                        };

                        introductionYear = ryzenGeneration switch
                        {
                            "1" => 2017,
                            "2" => 2018,
                            "3" => 2019,
                            "4" => 2020,
                            "5" => 2021,
                            "6" => 2022,
                            "7" => 2023,
                            _ => -1,
                        };

                        if (introductionYear == -1)
                        {
                            // 无法识别的代数，认为是新款
                            introductionYear = 2024;

                            // 如果代数大于2位，应当是后续10代往上的型号，预计2026年发布
                            if (ryzenGeneration.Length > 2) introductionYear = 2026;
                        }

                        return true;
                    }
                    else if (cpuName.Contains("epyc"))
                    {
                        Match match = Regex.Match(cpuName, @"epyc[\w()]* [\w]+([\d])[p]* ");
                        string epycGeneration = match.Groups[1].Value;
                        family = CpuFamily.AMD_Epyc;
                        platform = CpuPlatform.Desktop;

                        introductionYear = epycGeneration switch
                        {
                            "1" => 2017,
                            "2" => 2019,
                            "3" => 2021,
                            "4" => 2022,
                            _ => -1,
                        };

                        if (introductionYear == -1)
                        {
                            // 无法识别的代数，认为是新款
                            introductionYear = 2023;
                        }

                        return true;
                    }
                    else if (cpuName.Contains("athlon"))
                    {
                        // 2011年前后
                        family = CpuFamily.AMD_Athlon;
                        platform = CpuPlatform.Unknown;
                        introductionYear = 2011;
                        return true;
                    }
                    else if (cpuName.Contains("amd fx"))
                    {
                        // 2013年前后
                        family = CpuFamily.AMD_Fx;
                        platform = CpuPlatform.Unknown;
                        introductionYear = 2010;
                        return true;
                    }
                    else if (cpuName.Contains("opteron"))
                    {
                        // 服务器市场 2010年前后
                        family = CpuFamily.AMD_Opteron;
                        platform = CpuPlatform.Desktop;
                        introductionYear = 2010;
                        return true;
                    }
                    else if (cpuName.Contains("phenom"))
                    {
                        // 2010年前后
                        family = CpuFamily.AMD_Phenom;
                        platform = CpuPlatform.Unknown;
                        introductionYear = 2010;
                        return true;
                    }
                    else if (cpuName.Contains("sempron"))
                    {
                        // 笔记本市场 2006年前后
                        family = CpuFamily.AMD_Sempron;
                        platform = CpuPlatform.Mobile;
                        introductionYear = 2006;
                        return true;
                    }
                    else if (cpuName.Contains("turion"))
                    {
                        // 笔记本市场 2010年前后
                        family = CpuFamily.AMD_Turion;
                        platform = CpuPlatform.Mobile;
                        introductionYear = 2010;
                        return true;
                    }
                    else if (cpuName.Contains("duron"))
                    {
                        // 低端市场 2001年前后
                        family = CpuFamily.AMD_Duron;
                        platform = CpuPlatform.Unknown;
                        introductionYear = 2001;
                        return true;
                    }
                }
            }
            catch (Exception) { }

            // 未知型号
            family = CpuFamily.Unkonwn;
            platform = CpuPlatform.Unknown;
            introductionYear = 0;
            return false;
        }

    }
}
