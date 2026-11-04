using System;
using System.Collections.Generic;
using System.Threading;

// 게임을 고르면 그 게임에서 쓰는 그림을 미리(뒤에서) 읽어 둬서, 시작할 때 처음 화면이 멈칫하지 않게 해요 (자동 생성 목록)
internal static class Preload
{
	private static readonly Dictionary<string, string[]> L = new Dictionary<string, string[]> {
		{ "archery", "am_arrow,ar_cart,ar_hit1,ar_pa0,ar_pa2,ar_pb0,ar_pb1,ar_pb2,ar_quiv,ar_tgt,ar_wind".Split(',') },
		{ "bat", "bat0,bat1,bat2,br0,br1,br2,br3,br4,br5,br6,br7,bt0,bt1,bt2,bt3,bt4,bt5,bt6,bt7,n_bball,pc0,pc1,pc2,pc3,pc4,pc5,pc6,pc7,pr_glove,pr_helmet,pr_trophy".Split(',') },
		{ "bowl", "bwp0,bwp1,bwp2,bwp3,m_bw_pin".Split(',') },
		{ "catch", "bl0,bl1,bl2,bl3,bl4,bl5,blk_ice,bolt,cl0,cl1,cl2,cl3,cl4,cl5,cl_chalk,cl_rope,cloud".Split(',') },
		{ "climb", "cb0,cb1,cb2,cb3,cb4,cb5,cb6,cb7,chalk,cl_chalk,cl_rope,cp_carab,cp_helmet,cp_rope,dizzy,hd1,hd2,hd3,hd4,hd5,hd_brk,hd_chalk,hd_crk0,hd_crk1,rk".Split(',') },
		{ "complaint", "dp0,dp1,dp2,dp3,dp4,dp5".Split(',') },
		{ "cover", "barr1,crate,crate2,cs1,cs3,dizzy,fl1,fl2,fl3,fl_heart,g_pistol,g_rifle,k_bpist,k_brifle,k_pistol,k_rifle,k_shotgun,k_sniper,planks,s_hit,sc0,sc1,sc2,sc3,sc4,sc5,sc_flag,sc_red,sc_whis,sc_yel,sh0,sh1,sh2,sh3,shd0,shd1,shd2,shd3,shd4,shd5,shg0,shg1,shg2,shg3,sm0".Split(',') },
		{ "curl", "cu_broom,m_cl_blue,m_cl_red".Split(',') },
		{ "flap", "fl_bomb,fl_boom,fl_e0,fl_e1,fl_e2,fl_e3,fl_e4,fl_heart,fl_i0,fl_i1,fl_i2,fl_i3,fl_i4,fl_ob,fl_opt0,fl_opt1,n_fy1,n_fy2,n_fy3,n_fy4,n_fy5".Split(',') },
		{ "g2048", "tm0,tm1,tm2,tm3,tm4,tm5,tr_flag,tr_stop,ty0,ty1,ty2,ty3,ty4,ty5".Split(',') },
		{ "golf", "golf_pt0,golf_pt1,golf_pt2,golf_pt3,golf_pt4,golf_sw0,golf_sw1,golf_sw2,golf_sw3,golf_sw4,golf_sw5,kt1_8,kt3_2,kt3_3,kt7_2,kt7_8".Split(',') },
		{ "hoop", "bk_crowd,bk_s0,bk_s1,bk_s2".Split(',') },
		{ "ice", "cu_peng,ic1,ic2,ic_sl".Split(',') },
		{ "jump", "cstar,n_barr,n_cone,n_paper,of_clock".Split(',') },
		{ "kart", "kart_cpu0,kart_cpu1,kart_cpu2,kart_cpu3,kart_cpu4,kart_me0,kart_me_air,kart_me_ban,kart_me_bd,kart_me_brk,kart_me_bst,kart_me_bub,kart_me_bump,kart_me_d15,kart_me_d30,kart_me_d45,kart_me_dc,kart_me_dh,kart_me_dz,kart_me_f,kart_me_fd,kart_me_land,kart_me_rev,kart_me_s1,kart_me_s2,kart_me_sad,kart_me_side,kart_me_wall,kart_me_win,kfx_10,kfx_11,kfx_2,kfx_3,kfx_4,kfx_5,kfx_6,kfx_7,kfx_9,kp_0,kp_1,kp_2,kp_3,kp_4,kp_5,kp_6,kp_7,kt_balloon,kt_boost,kt_get,kt_giant".Split(',') },
		{ "kick", "gk0,gk1,gk2,gk3,gk4,gk5,gk6,kk0,kk1,kk2,kk3,kk4,kk5,kk6,kk7,n_sball,sc_flag,sc_red,sc_whis,sc_yel,wl0,wl1,wl2".Split(',') },
		{ "memory", "mem".Split(',') },
		{ "mole", "coins,hm0,hm1,hm2,hm3,m_mo1,m_mo2,m_mo3,m_mo4".Split(',') },
		{ "puzzle", "am_bomb,blk_ice,pet_eat0,pet_happy0,pet_sit0,pet_stand0,pet_yawn0".Split(',') },
		{ "rhythm", "rs0,rs1,rs2,rs3,rs_hit,rs_puff".Split(',') },
		{ "rtd", "coins,ecu0,ecu1,ecu2,ecu3,edoc0,edoc1,edoc2,edoc3,emg0,emg1,emg2,emg3,pet_fly0,pet_kick0,pet_sip0,pet_stand0,pet_wave0,s2x0,s2x1,s2x2,s2x3,s3x0,s3x1,s3x2,s3x3,s4x0,s4x1,s4x2,s4x3,s5x0,s5x1,s5x2,s5x3,wcof0,wfire0,wlig0,wpla0,wsta0".Split(',') },
		{ "run", "ds0,ds1,ds2,ds3,ds4,ds5,rn0,rn1,rn10,rn11,rn2,rn3,rn4,rn5,rn6,rn7,rn8,rn9,tr_flag".Split(',') },
		{ "shell", "fo0,fo1,fo2,fo3,fo_paper,fo_stamp".Split(',') },
		{ "shoot", "cy1,cy2,k_shotgun".Split(',') },
		{ "sling", "am_bomb,am_bounce,am_fire1,am_fire2,am_ice1,am_pierce,ice_sh,shd0,shd1,shd2,shd3,shd4,shd5".Split(',') },
		{ "stamp", "dp0,dp1,dp2,dp3,dp4,dp5,of_clip,so_ok,sp1,sp2".Split(',') },
		{ "surv", "cu_ball,cu_crys,dz0,dz1,dz2,dz3,dz4,dz5,e_atk,ebm0,ebm1,ebm2,ebm3,ecu0,ecu1,ecu2,ecu3,edoc0,edoc1,edoc2,edoc3,emg0,emg1,emg2,emg3,of_mug,of_note,of_paper,of_pc,of_stap,of_tel,op0,opa0,opa1,opa2,opa3,opb0,opb1,opb2,opb3,opc0,opc1,opc2,opc3,opd0,opd1,opd2,opd3,ops0,ops1,ops2,ops3,opx0,opx1,opx2,opx3,ow_board,ow_cab,ow_cert,ow_clock,ow_lamp,ow_plant,pj1,pj2,pj4,pj6,pj7,pj9,shg0,shg1,shg2,shg3,sn0,sn1,sn2,sn3,sn4,sn5,so_ok,svc0,svc1,svc2,sve0,sve1,sve2,sve3,sve4,sve5,svg0,svg1,svg4,svg5,svi0,svi1,svi2,svi3,svi4,svi5,svp0,svp1,svp10,svp2,svp3,svp4,svp5,svp6,svp7,svp8,svp9,svp_up,svw0,svw1,svw2,svw3,svw4,svw5,svw6,svw7,svw8,svw9,tm0,ui_cup,waur0,wboo4,wbub4,wcof4,wcof5,wexp0,wfire4,wkb0,wkb1,wkb2,wkb3,wkb4,wkb5,wkf0,wkf1,wkf2,wkf3,wkf4,wkf5,wks0,wks1,wks2,wks3,wks4,wks5,wlas20,wlas21,wlas22,wlas23,wlas24,wlas25,wlas26,wlas27,wlig0,wlig4,wpla4,wsl0,wsl1,wsl2,wsta0,wsta4,wstp4,za1,zad0,zad1,zad2,zad3,zad4,zad5".Split(',') },
		{ "time", "tm1,tm2,tm3".Split(',') },
		{ "tower2", "cf,ch0,ch1,ch2,ch3,ch4,ch5,cray,crn,cstar,glow,medal,rib,ui_cup,ui_heart,ui_medal,ui_st1,ui_st2".Split(',') },
		{ "type", "ty0,ty1,ty2,ty3,ty4,ty5".Split(',') },
		{ "volley", "pet_toy_ball,vb_bg,vb_fx0,vb_fx1,vb_m0,vb_m1,vb_m2,vb_m3,vb_m4,vb_m5,vb_m6,vb_m7,vb_o0,vb_o1,vb_o2,vb_o3,vb_o4,vb_o5,vb_o6,vb_o7".Split(',') },
	};
	private static readonly HashSet<string> done = new HashSet<string>(), fin = new HashSet<string>();
	public static bool Ready(string key) { lock (done) return !L.ContainsKey(key) || fin.Contains(key) || !done.Contains(key); }
	public static void Start(string key)
	{
		try
		{
			string[] l; lock (done) { if (!L.TryGetValue(key, out l) || !done.Add(key)) return; }
			Thread t = new Thread(delegate() { try { foreach (string n in l) { Spr.Get("ms_" + n); Thread.Sleep(1); } } catch { } lock (done) fin.Add(key); });
			t.IsBackground = true; t.Priority = ThreadPriority.BelowNormal; t.Start();
		}
		catch { }
	}
	public static void Forget() { lock (done) { done.Clear(); fin.Clear(); } }
}
