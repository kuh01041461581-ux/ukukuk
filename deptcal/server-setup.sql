-- ============================================================
-- 부서 일정 달력 - 서버 설정 (관리자가 Supabase > SQL Editor 에서 한 번만 실행)
-- 여러 번 실행해도 안전합니다.
-- 추가되는 것: 완료(취소선) / 개인 일정(본인에게만 보임) / 첨부파일(공문) / PC 교체 시 계정 이전
-- ============================================================

-- 1) 열 추가
alter table public.events  add column if not exists done  boolean not null default false;
alter table public.events  add column if not exists scope text    not null default 'dept';
alter table public.events  add column if not exists files jsonb   not null default '[]'::jsonb;
alter table public.events  drop constraint if exists events_scope_check;
alter table public.events  add constraint events_scope_check check (scope in ('dept','personal'));
alter table public.members add column if not exists claim_hash text;

-- 2) 개인 일정은 작성자 본인에게만 보이게 (부서 일정은 전원에게 보임)
drop policy if exists events_read on public.events;
create policy events_read on public.events for select to authenticated
  using (scope = 'dept' or owner_id = auth.uid());

-- 3) PC 교체 시 계정 이전 함수 (복구 파일의 비밀값을 아는 사람만 이전 가능)
create or replace function public.claim_account(old_id uuid, secret text)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
begin
  if auth.uid() is null then return false; end if;
  if old_id = auth.uid() then return true; end if;
  if not exists (
    select 1 from public.members
    where user_id = old_id
      and claim_hash = encode(sha256(convert_to(secret, 'UTF8')), 'hex')
  ) then
    return false;
  end if;
  delete from public.members where user_id = auth.uid();
  update public.events  set owner_id = auth.uid() where owner_id = old_id;
  update public.members set user_id  = auth.uid() where user_id  = old_id;
  return true;
end;
$$;
revoke all on function public.claim_account(uuid, text) from public, anon;
grant execute on function public.claim_account(uuid, text) to authenticated;

-- 4) 첨부파일 저장소 (비공개, 파일당 20MB)
insert into storage.buckets (id, name, public, file_size_limit)
values ('attachments', 'attachments', false, 20971520)
on conflict (id) do update set public = false, file_size_limit = 20971520;

drop policy if exists att_read   on storage.objects;
drop policy if exists att_insert on storage.objects;
drop policy if exists att_delete on storage.objects;

create policy att_read on storage.objects for select to authenticated
  using (bucket_id = 'attachments');
create policy att_insert on storage.objects for insert to authenticated
  with check (bucket_id = 'attachments' and (storage.foldername(name))[1] = auth.uid()::text);
create policy att_delete on storage.objects for delete to authenticated
  using (bucket_id = 'attachments' and (storage.foldername(name))[1] = auth.uid()::text);
