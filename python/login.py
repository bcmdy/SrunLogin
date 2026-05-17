#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Srun 校园网认证工具
=================
用法:
    登录:   python login.py login -u 账号 -p 密码 --url http://网关地址
    状态:  python login.py info -u 账号 --url http://网关地址
    登出:  python login.py logout -u 账号 -p 密码 --url http://网关地址

示例:
    python login.py login -u 您的账号 -p 密码 --url http://1.1.1.1
    python login.py info -u 您的账号 --url http://1.1.1.1
    python login.py logout -u 您的账号 -p 密码 --url http://1.1.1.1

参数:
    --url      认证服务器地址 (必需)
    -u/--username  用户名
    -p/--password  密码
    --ip      指定IP (可选)
    --ac-id    AC ID (可选)
    --domain   域 (可选)
"""
import sys
print("[诊断] 脚本开始加载...", flush=True)

import urllib.request
import urllib.parse
import http.cookiejar
import hmac
import hashlib
import json
import base64
import re
import argparse
import ssl
import random
import time
from datetime import datetime
from collections import Counter

print("[诊断] 模块导入完成 (纯标准库，零依赖)", flush=True)

ssl._create_default_https_context = ssl._create_unverified_context


class SrunBase64:
    STD_ALPHA = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
    CUSTOM_ALPHA = 'LVoJPiCN2R8G90yg+hmFHuacZ1OWMnrsSTXkYpUq/3dlbfKwv6xztjI7DeBE45QA'

    @classmethod
    def encode(cls, data: bytes) -> str:
        encoded = base64.b64encode(data).decode()
        return encoded.translate(str.maketrans(cls.STD_ALPHA, cls.CUSTOM_ALPHA))


def xxtea_encrypt(plaintext: str, key: str) -> str:
    def str_to_uints(s: str, add_len: bool):
        n = len(s)
        v = []
        for i in range(0, n, 4):
            val = ord(s[i]) if i < n else 0
            val |= (ord(s[i + 1]) << 8) if i + 1 < n else 0
            val |= (ord(s[i + 2]) << 16) if i + 2 < n else 0
            val |= (ord(s[i + 3]) << 24) if i + 3 < n else 0
            v.append(val & 0xFFFFFFFF)
        if add_len:
            v.append(n)
        return v

    def uints_to_str(v: list) -> str:
        chars = []
        for num in v:
            chars.append(chr(num & 0xff))
            chars.append(chr((num >> 8) & 0xff))
            chars.append(chr((num >> 16) & 0xff))
            chars.append(chr((num >> 24) & 0xff))
        return ''.join(chars)

    v = str_to_uints(plaintext, True)
    k = str_to_uints(key, False)
    while len(k) < 4:
        k.append(0)

    n = len(v) - 1
    if n < 1:
        return ""

    z = v[n]
    y = v[0]
    delta = 0x9E3779B9
    q = 6 + 52 // (n + 1)
    d = 0

    while q > 0:
        q -= 1
        d = (d + delta) & 0xFFFFFFFF
        e = (d >> 2) & 3
        for p in range(n):
            y = v[p + 1]
            m = ((z & 0xFFFFFFFF) >> 5) ^ ((y << 2) & 0xFFFFFFFF)
            m += (((y & 0xFFFFFFFF) >> 3) ^ ((z << 4) & 0xFFFFFFFF)) ^ (d ^ y)
            m += (k[(p & 3) ^ e] ^ z) & 0xFFFFFFFF
            v[p] = (v[p] + m) & 0xFFFFFFFF
            z = v[p]
        y = v[0]
        m = ((z & 0xFFFFFFFF) >> 5) ^ ((y << 2) & 0xFFFFFFFF)
        m += (((y & 0xFFFFFFFF) >> 3) ^ ((z << 4) & 0xFFFFFFFF)) ^ (d ^ y)
        m += (k[(n & 3) ^ e] ^ z) & 0xFFFFFFFF
        v[n] = (v[n] + m) & 0xFFFFFFFF
        z = v[n]

    return uints_to_str(v)


class SrunPortal:
    def __init__(self, auth_url, username, password, ac_id=None, ip=None, domain=''):
        print(f"[诊断] 初始化 Portal: url={auth_url}, user={username}", flush=True)
        self.auth_url = auth_url.rstrip('/')
        self.username = username
        self.password = password
        self.domain = domain
        self.ac_id = ac_id
        self.ip = ip
        self.portal_page = 'srun_portal_pc'
        
        self.cookiejar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookiejar)
        )
        self.headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36 Edg/135.0.0.0',
            'Accept': 'text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01',
            'Accept-Language': 'zh-CN,zh;q=0.9',
            'X-Requested-With': 'XMLHttpRequest',
        }

    def _update_referer(self):
        self.headers['Referer'] = f"{self.auth_url}/{self.portal_page}?ac_id={self.ac_id}&theme=pro"

    @staticmethod
    def _parse_response(text: str):
        text = text.strip()
        if not text:
            raise ValueError("空响应")
        
        if text == 'ok':
            return {"error": "ok"}
        if text == 'not_online_error':
            return {"error": "not_online_error"}
        if text == 'login_error':
            return {"error": "login_error"}
        
        if text.startswith('challenge='):
            return {"error": "ok", "challenge": text.split('=', 1)[1]}
        
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            pass
        
        m = re.search(r'[^(]*\((.*)\)\s*;?\s*$', text, re.DOTALL)
        if m:
            try:
                return json.loads(m.group(1))
            except json.JSONDecodeError:
                pass
        
        if ',' in text and not text.startswith('<'):
            parts = text.split(',')
            result = {
                '_raw': text,
                '_csv': parts,
                'error': 'ok',
            }
            if len(parts) > 0:
                result['user_name'] = parts[0].strip()
            if len(parts) > 8:
                result['online_ip'] = parts[8].strip()
            if len(parts) > 6:
                try: result['sum_bytes'] = int(parts[6])
                except: pass
            if len(parts) > 4:
                try: result['sum_seconds'] = int(parts[4])
                except: pass
            return result
        
        raise ValueError(f"无法解析响应: {text[:200]}")

    def _get(self, path: str, params: dict = None, jsonp: bool = False):
        url = self.auth_url + path
        if params is None:
            params = {}
        if jsonp:
            params['callback'] = f'jQuery{random.randint(100000000000000000000, 999999999999999999999)}_{int(time.time() * 1000)}'
            params['_'] = str(int(time.time() * 1000))
        if params:
            url += '?' + urllib.parse.urlencode(params)
        
        self._update_referer()
        print(f"[诊断] 请求: {url[:130]}...", flush=True)
        req = urllib.request.Request(url, headers=self.headers)
        try:
            with self.opener.open(req, timeout=15) as resp:
                text = resp.read().decode('utf-8')
                print(f"[诊断] 响应: {text[:200]}", flush=True)
                return self._parse_response(text)
        except urllib.error.HTTPError as e:
            body = e.read().decode('utf-8', errors='ignore')
            print(f"[诊断] HTTP {e.code} 错误: {body[:200]}", flush=True)
            raise
        except Exception as e:
            print(f"[诊断] 请求异常: {type(e).__name__}: {e}", flush=True)
            raise

    def _fetch_html(self, url_path: str) -> tuple:
        req = urllib.request.Request(self.auth_url + url_path, headers={
            'User-Agent': self.headers['User-Agent'],
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
        })
        with self.opener.open(req, timeout=10) as resp:
            return resp.read().decode('utf-8'), resp.geturl()

    def _extract_ac_id_from_html(self, html: str) -> str:
        """从 HTML 中提取 ac_id，收集所有候选，优先非 1 的值"""
        candidates = []
        
        # 1. input 元素
        for m in re.finditer(r'<input[^>]*id=["\']ac_id["\'][^>]*value=["\'](\d+)["\']', html):
            candidates.append(m.group(1))
        for m in re.finditer(r'<input[^>]*value=["\'](\d+)["\'][^>]*id=["\']ac_id["\']', html):
            candidates.append(m.group(1))
        
        # 2. JS 变量
        for m in re.finditer(r"var\s+ac_id\s*=\s*['\"](\d+)['\"]", html):
            candidates.append(m.group(1))
        for m in re.finditer(r"var\s+acid\s*=\s*['\"]?(\d+)['\"]?", html):
            candidates.append(m.group(1))
        
        # 3. JSON 格式（CONFIG 等）
        for m in re.finditer(r'["\']?ac_id["\']?\s*:\s*["\']?(\d+)["\']?', html):
            candidates.append(m.group(1))
        for m in re.finditer(r'["\']?acid["\']?\s*:\s*["\']?(\d+)["\']?', html):
            candidates.append(m.group(1))
        
        # 4. URL 参数
        for m in re.finditer(r'[?&]ac_id=(\d+)', html):
            candidates.append(m.group(1))
        
        # 5. index_X.html 形式
        for m in re.finditer(r'index_(\d+)\.html', html):
            candidates.append(m.group(1))
        
        # 6. srun_portal_pc?ac_id=X
        for m in re.finditer(r'srun_portal_pc\?ac_id=(\d+)', html):
            candidates.append(m.group(1))
        
        if not candidates:
            return None
            
        counts = Counter(candidates)
        print(f"[诊断] HTML 中发现 ac_id 候选: {dict(counts)}", flush=True)
        
        # 优先选出现次数最多的非 1 值
        non_one = [c for c in counts if c != '1']
        if non_one:
            best = Counter(non_one).most_common(1)[0][0]
            print(f"[诊断] 选择非默认 ac_id: {best}", flush=True)
            return best
        return counts.most_common(1)[0][0]

    def _extract_ip_from_html(self, html: str) -> str:
        patterns = [
            r'<input[^>]*id=["\']ip["\'][^>]*value=["\']([\d.]+)["\']',
            r'<input[^>]*value=["\']([\d.]+)["\'][^>]*id=["\']ip["\']',
            r'ip\s*[:=]\s*["\']([\d.]+)["\']',
            r'userip\s*[:=]\s*["\']([\d.]+)["\']',
            r'client_ip\s*[:=]\s*["\']([\d.]+)["\']',
            r'online_ip\s*[:=]\s*["\']([\d.]+)["\']',
            r'"ip"\s*:\s*"([\d.]+)"',
            r'"client_ip"\s*:\s*"([\d.]+)"',
            r'"online_ip"\s*:\s*"([\d.]+)"',
            r'var\s+ip\s*=\s*["\']?([\d.]+)["\']?',
        ]
        for pat in patterns:
            m = re.search(pat, html)
            if m:
                return m.group(1)
        return None

    def detect_info(self):
        print("[诊断] 开始探测 IP/AC_ID...", flush=True)
        if self.ac_id and self.ip:
            print(f"[诊断] 已提供 IP={self.ip}, AC_ID={self.ac_id}", flush=True)
            return
        
        # ===== 1. 访问首页，解析 ac_id 和 IP =====
        try:
            html, final_url = self._fetch_html('/')
            print(f"[诊断] 首页最终 URL: {final_url}", flush=True)
            
            ac_id_from_html = self._extract_ac_id_from_html(html)
            if ac_id_from_html:
                self.ac_id = self.ac_id or ac_id_from_html
            
            if not self.ip:
                ip_from_html = self._extract_ip_from_html(html)
                if ip_from_html:
                    self.ip = ip_from_html
                    print(f"[诊断] 从首页 HTML 提取 IP: {self.ip}", flush=True)
                    
        except Exception as e:
            print(f"[诊断] 首页探测失败: {e}", flush=True)

        # ===== 2. 尝试 ac_detect 接口获取重定向配置（最可能包含真实 ac_id）=====
        if not self.ac_id or self.ac_id == '1':
            try:
                print("[诊断] 尝试 ac_detect 接口获取真实 ac_id...", flush=True)
                data = self._get('/v1/srun_portal_detect')
                print(f"[诊断] ac_detect 返回: {json.dumps(data, ensure_ascii=False)}", flush=True)
                
                if data.get('Redirect'):
                    redirect_url = data.get('Pc') or data.get('Mobile')
                    if redirect_url:
                        m = re.search(r'[?&]ac_id=(\d+)', redirect_url)
                        if m:
                            self.ac_id = m.group(1)
                            print(f"[诊断] 从 ac_detect 重定向 URL 获取 ac_id: {self.ac_id}", flush=True)
                
                # 有些版本直接返回 ac_id 字段
                if not self.ac_id or self.ac_id == '1':
                    if 'ac_id' in data:
                        self.ac_id = str(data['ac_id'])
                        print(f"[诊断] 从 ac_detect 数据获取 ac_id: {self.ac_id}", flush=True)
                    elif 'acid' in data:
                        self.ac_id = str(data['acid'])
                        print(f"[诊断] 从 ac_detect 数据获取 acid: {self.ac_id}", flush=True)
                        
            except Exception as e:
                print(f"[诊断] ac_detect 失败: {e}", flush=True)

        # ===== 3. 访问 srun_portal_pc（不带参数），解析其 HTML =====
        if not self.ac_id or self.ac_id == '1':
            try:
                print("[诊断] 尝试访问 srun_portal_pc 获取真实 ac_id...", flush=True)
                html, final_url = self._fetch_html('/srun_portal_pc')
                print(f"[诊断] srun_portal_pc 最终 URL: {final_url}", flush=True)
                
                # 从最终 URL 提取 ac_id
                m = re.search(r'[?&]ac_id=(\d+)', final_url)
                if m:
                    self.ac_id = m.group(1)
                    print(f"[诊断] 从 srun_portal_pc URL 提取 ac_id: {self.ac_id}", flush=True)
                
                # 从 HTML 深度提取
                ac_id_from_html = self._extract_ac_id_from_html(html)
                if ac_id_from_html and ac_id_from_html != '1':
                    self.ac_id = ac_id_from_html
                
                if not self.ip:
                    ip_from_html = self._extract_ip_from_html(html)
                    if ip_from_html:
                        self.ip = ip_from_html
                        print(f"[诊断] 从 srun_portal_pc HTML 提取 IP: {self.ip}", flush=True)
                        
            except Exception as e:
                print(f"[诊断] srun_portal_pc 探测失败: {e}", flush=True)

        # ===== 4. 尝试候选 ac_id 列表（通过 get_challenge 测试有效性）=====
        if not self.ac_id or self.ac_id == '1':
            # 常见校园网 ac_id 候选
            candidates = ['143', '2', '3', '5', '10', '15', '20', '100']
            print(f"[诊断] 尝试候选 ac_id 列表: {candidates}", flush=True)
            for test_ac_id in candidates:
                try:
                    params = {
                        'username': self.username + self.domain,
                        'ip': self.ip or '0.0.0.0',
                    }
                    # 临时修改 ac_id 测试
                    old_ac_id = self.ac_id
                    self.ac_id = test_ac_id
                    data = self._get('/cgi-bin/get_challenge', params, jsonp=True)
                    self.ac_id = old_ac_id
                    
                    if data.get('error') == 'ok' and 'challenge' in data:
                        # 进一步测试：用这个 ac_id 尝试登录（但用错误密码，看错误类型）
                        # 如果返回 Nas type not found，说明 ac_id 错误
                        # 如果返回其他错误（如密码错误），说明 ac_id 可能正确
                        # 这里简化：只要 get_challenge 成功就认为可能有效
                        print(f"[诊断] ac_id={test_ac_id} 的 get_challenge 成功，暂定为候选", flush=True)
                        self.ac_id = test_ac_id
                        break
                except Exception as e:
                    print(f"[诊断] ac_id={test_ac_id} 测试失败: {e}", flush=True)

        # ===== 5. JSONP 模式 rad_user_info 获取 IP =====
        if not self.ip:
            try:
                print("[诊断] 尝试 JSONP 模式 rad_user_info 获取 IP...", flush=True)
                data = self._get('/cgi-bin/rad_user_info', jsonp=True)
                if 'client_ip' in data:
                    self.ip = data['client_ip']
                    print(f"[诊断] 从 JSONP rad_user_info 获取 IP: {self.ip}", flush=True)
                if 'online_ip' in data:
                    self.ip = self.ip or data['online_ip']
            except Exception as e:
                print(f"[诊断] JSONP rad_user_info 失败: {e}", flush=True)

        # ===== 6. 最终校验 =====
        if not self.ip:
            raise ValueError("无法自动获取本机 IP，请手动指定 --ip，例如: --ip 10.189.150.176")
        if not self.ac_id:
            print("[警告] 无法自动获取 ac_id，使用默认值 1", flush=True)
            self.ac_id = '1'
            
        print(f"[诊断] 探测结果: IP={self.ip}, AC_ID={self.ac_id}, portal_page={self.portal_page}", flush=True)

    def get_challenge(self) -> str:
        params = {
            'username': self.username + self.domain,
            'ip': self.ip,
        }
        data = self._get('/cgi-bin/get_challenge', params, jsonp=True)
        if 'challenge' in data:
            return data['challenge']
        if data.get('error') == 'ok':
            print("[诊断] get_challenge 返回 ok 无 challenge，尝试无 callback 模式", flush=True)
            data2 = self._get('/cgi-bin/get_challenge', params, jsonp=False)
            if 'challenge' in data2:
                return data2['challenge']
            print("[诊断] 服务器未提供 challenge，可能老版本无需 token", flush=True)
            return ''
        raise RuntimeError(f"获取 challenge 失败: {data}")

    def login(self):
        print(f"[登录] 账号: {self.username}, IP: {self.ip}, AC_ID: {self.ac_id}", flush=True)
        token = self.get_challenge()
        print(f"[登录] 获取 token: {token[:8] if token else '(无)'}...", flush=True)

        params_list = [
            ('action', 'login'),
            ('username', self.username + self.domain),
            ('password', ''),
            ('os', 'Windows 10'),
            ('name', 'Windows'),
            ('double_stack', '0'),
            ('chksum', ''),
            ('info', ''),
            ('ac_id', self.ac_id),
            ('ip', self.ip),
            ('n', '200'),
            ('type', '1'),
        ]

        if token:
            hmd5 = hmac.new(token.encode(), self.password.encode(), hashlib.md5).hexdigest()
            info = {
                "username": self.username + self.domain,
                "password": self.password,
                "ip": self.ip,
                "acid": self.ac_id,
                "enc_ver": "srun_bx1"
            }
            info_str = json.dumps(info, separators=(',', ':'))
            encrypted = xxtea_encrypt(info_str, token)
            i = '{SRBX1}' + SrunBase64.encode(encrypted.encode('latin1'))

            chkstr = token + self.username + self.domain
            chkstr += token + hmd5
            chkstr += token + self.ac_id
            chkstr += token + self.ip
            chkstr += token + '200'
            chkstr += token + '1'
            chkstr += token + i
            chksum = hashlib.sha1(chkstr.encode()).hexdigest()

            params_list[2] = ('password', '{MD5}' + hmd5)
            params_list[6] = ('chksum', chksum)
            params_list[7] = ('info', i)
        else:
            print("[登录] 使用老版本明文密码模式", flush=True)
            params_list[2] = ('password', self.password)

        params = dict(params_list)
        return self._get('/cgi-bin/srun_portal', params, jsonp=True)

    def info(self):
        return self._get('/cgi-bin/rad_user_info', jsonp=True)

    def get_expire_time(self):
        try:
            return self._get('/v1/srun_portal_expire_time')
        except Exception as e:
            print(f"[诊断] 获取到期时间失败: {e}", flush=True)
            return None

    def logout(self):
        params = {
            'action': 'logout',
            'username': self.username + self.domain,
            'ip': self.ip,
            'ac_id': self.ac_id,
        }
        return self._get('/cgi-bin/srun_portal', params, jsonp=True)


def format_flow(bytes_val, mode=1024):
    if not bytes_val or bytes_val == '0':
        return '0 B'
    units = ['B', 'KB', 'MB', 'GB', 'TB']
    val = float(bytes_val)
    idx = 0
    while val >= mode and idx < len(units) - 1:
        val /= mode
        idx += 1
    return f"{val:.2f} {units[idx]}"


def format_time(seconds_val):
    if not seconds_val:
        return '0 秒'
    try:
        s = int(seconds_val)
    except (ValueError, TypeError):
        return str(seconds_val)
    d, s = divmod(s, 86400)
    h, s = divmod(s, 3600)
    m, s = divmod(s, 60)
    parts = []
    if d: parts.append(f"{d}天")
    if h: parts.append(f"{h}小时")
    if m: parts.append(f"{m}分")
    if s or not parts: parts.append(f"{s}秒")
    return ''.join(parts)


def show_result(result, info_data=None, expire_data=None):
    print("\n" + "="*50, flush=True)
    print("【认证服务器原始响应】", flush=True)
    print(json.dumps(result, indent=2, ensure_ascii=False), flush=True)

    is_ok = result.get('error') == 'ok' or result.get('code') == 0
    suc_msg = result.get('suc_msg', '')
    
    if is_ok and suc_msg == 'ip_already_online_error':
        print("\n[!] 当前 IP 已在线，无需重复登录", flush=True)
        print(f"IP: {result.get('online_ip', 'N/A')}", flush=True)
        return

    if not is_ok:
        print("\n[✗] 登录失败", flush=True)
        err = result.get('error_msg') or result.get('message') or result.get('ecode') or '未知'
        print(f"错误: {err}", flush=True)
        if result.get('ecode') == 'E2901':
            print("提示: 账号或密码错误", flush=True)
        elif result.get('ecode') == 'E2620':
            print("提示: 在线设备数量超限", flush=True)
        elif result.get('error_msg') == 'ip_already_online_error':
            print("提示: 当前 IP 已在线", flush=True)
        elif result.get('error') == 'auth_info_error':
            print("提示: 认证信息加密错误（info/chksum 不匹配）", flush=True)
        elif result.get('error') == 'login_error' and 'nas' in err.lower():
            print("提示: NAS 类型未找到，可能是 ac_id 不正确", flush=True)
        return

    print("\n[✓] 登录成功", flush=True)
    if info_data and info_data.get('error') != 'not_online_error':
        print(f"{'账号:':<12} {info_data.get('user_name', 'N/A')}", flush=True)
        print(f"{'IP:':<12} {info_data.get('online_ip') or info_data.get('client_ip') or 'N/A'}", flush=True)
        print(f"{'MAC:':<12} {info_data.get('user_mac', 'N/A')}", flush=True)
        sb = info_data.get('sum_bytes')
        if sb:
            print(f"{'已用流量:':<12} {format_flow(sb)}", flush=True)
        ss = info_data.get('sum_seconds')
        if ss:
            print(f"{'已用时长:':<12} {format_time(ss)}", flush=True)
        bal = info_data.get('user_balance')
        if bal is not None:
            try:
                print(f"{'余额:':<12} ¥{float(bal):.2f}", flush=True)
            except Exception:
                pass
        if expire_data and expire_data.get('code') == 0:
            ts = expire_data.get('data')
            if ts and ts != 0:
                dt = datetime.fromtimestamp(int(ts))
                print(f"{'到期时间:':<12} {dt.strftime('%Y-%m-%d %H:%M:%S')}", flush=True)


def main():
    print("[诊断] main() 开始执行...", flush=True)
    parser = argparse.ArgumentParser(description='Srun 校园网认证 (纯标准库版)')
    parser.add_argument('action', choices=['login', 'logout', 'info'])
    parser.add_argument('-u', '--username', required=True)
    parser.add_argument('-p', '--password')
    parser.add_argument('--url', default='http://10.0.0.1')
    parser.add_argument('--ip')
    parser.add_argument('--ac-id')
    parser.add_argument('--domain', default='')

    args = parser.parse_args()
    print(f"[诊断] 参数解析完成: action={args.action}", flush=True)

    if args.action == 'login' and not args.password:
        parser.error('login 需要提供 -p/--password')

    portal = SrunPortal(
        auth_url=args.url,
        username=args.username,
        password=args.password or '',
        ac_id=args.ac_id,
        ip=args.ip,
        domain=args.domain
    )

    if args.action == 'login':
        portal.detect_info()
        result = portal.login()
        info_data = None
        expire_data = None
        try:
            info_data = portal.info()
        except Exception as e:
            print(f"[诊断] 查询在线信息失败: {e}", flush=True)
        try:
            expire_data = portal.get_expire_time()
        except Exception:
            pass
        show_result(result, info_data, expire_data)
    elif args.action == 'info':
        portal.detect_info()
        result = portal.info()
        print(json.dumps(result, indent=2, ensure_ascii=False), flush=True)
        if '_csv' in result:
            print(f"\nCSV 解析: 账号={result.get('user_name')}, IP={result.get('online_ip')}, 已用流量={format_flow(result.get('sum_bytes'))}, 已用时长={format_time(result.get('sum_seconds'))}")
    elif args.action == 'logout':
        portal.detect_info()
        result = portal.logout()
        print(json.dumps(result, indent=2, ensure_ascii=False), flush=True)
        if result.get('error') == 'ok' or result.get('code') == 0:
            print("\n[✓] 注销成功", flush=True)
        else:
            err = result.get('error_msg') or result.get('message') or '未知'
            print(f"\n[✗] 注销失败: {err}", flush=True)


if __name__ == '__main__':
    try:
        main()
    except BaseException as e:
        print(f"\n[致命错误] {type(e).__name__}: {e}", flush=True)
        import traceback
        traceback.print_exc()
        sys.exit(1)